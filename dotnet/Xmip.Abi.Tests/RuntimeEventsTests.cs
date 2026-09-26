using System.Runtime.InteropServices;
using Xmip.Abi.Operate;

namespace Xmip.Abi.Tests;

/// <summary>
/// <see cref="RuntimeEvents"/> crosses section 11 of <c>xmip_operate.h</c> to
/// the runtime this estate built and back: an Event published into this
/// process reaches a subscriber whole, drained or called back. Which Events
/// match and who may see them is <c>xmip-core-event</c>'s and tested there;
/// these prove the crossing. Every subscription audits into a temporary
/// directory of the test's own, never the operating system's log.
/// </summary>
public sealed class RuntimeEventsTests
{
    /// <summary>A Party's UUID, as a subscriber in this process is.</summary>
    internal const string Party = "0198a3c4-0000-7000-8000-000000000042";

    private static RuntimeEvents Events => RuntimeRulesTests.Rules.Events;

    [Fact]
    public void TheShapesAreTheHeadersOnASixtyFourBitProcess()
    {
        Assert.True(Environment.Is64BitProcess, "the estate builds 64-bit");

        // Thirteen strings of 16 bytes, a time, two enums, a pointer and a length.
        Assert.Equal(192, Marshal.SizeOf<XmipEvent>());
        Assert.Equal(64, Marshal.SizeOf<XmipEventFilter>());
    }

    [Fact]
    public void APublishedEventIsDrainedWholeAndTheBatchIsFreed()
    {
        using Audited audited = new();
        using EventSubscription subscription = Events.Subscribe(
            "Xmip.Abi.Tests", audited.Directory, Party,
            new EventFilter { Scope = audited.Scope, Outcomes = [EventOutcome.Failure] });

        int delivered = Events.Publish(new EventRecord
        {
            Type = "se.xmip.receive.failure",
            Action = EventAction.Receive,
            Outcome = EventOutcome.Failure,
            Scope = audited.Scope + "/node/n/receive/orders",
            Journey = "0198a3c4-0000-7000-8000-000000000001",
            Endpoint = "tcp://0.0.0.0:5000",
            Artifact = "orders",
            Diagnostics = new Dictionary<string, string> { ["status"] = "refused", ["ö"] = "å" },
        });

        Assert.Equal(1, delivered);

        EventDelivery delivery = subscription.Next(TimeSpan.FromSeconds(2), 16);
        EventRecord heard = Assert.Single(delivery.Events);

        Assert.Equal(0ul, delivery.Refused);
        Assert.Equal(36, heard.Id.Length);
        Assert.Equal("se.xmip.receive.failure", heard.Type);
        Assert.Equal(EventAction.Receive, heard.Action);
        Assert.Equal(EventOutcome.Failure, heard.Outcome);
        Assert.Equal(audited.Scope + "/node/n/receive/orders", heard.Scope);
        Assert.Equal("0198a3c4-0000-7000-8000-000000000001", heard.Journey);
        Assert.Equal("orders", heard.Artifact);
        Assert.Equal(string.Empty, heard.Message);
        Assert.Equal("refused", heard.Diagnostics["status"]);
        Assert.Equal("å", heard.Diagnostics["ö"]);
        Assert.InRange(heard.Time, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow);

        Assert.Empty(subscription.Next(TimeSpan.FromMilliseconds(1), 16).Events);
    }

    [Fact]
    public void AnEventTheFilterDoesNotAskForIsNotDelivered()
    {
        using Audited audited = new();
        using EventSubscription subscription = Events.Subscribe(
            "Xmip.Abi.Tests", audited.Directory, Party,
            new EventFilter { Scope = audited.Scope, Outcomes = [EventOutcome.Failure] });

        Assert.Equal(0, Events.Publish(audited.Raised(EventOutcome.Success)));
        Assert.Empty(subscription.Next(TimeSpan.Zero, 16).Events);
    }

    [Fact]
    public async Task AListenerIsCalledBackWithEachEvent()
    {
        using Audited audited = new();
        TaskCompletionSource<EventRecord> heard =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        using EventSubscription subscription = Events.Listen(
            "Xmip.Abi.Tests", audited.Directory, Party, raised => heard.TrySetResult(raised),
            new EventFilter { Scope = audited.Scope });

        Assert.True(subscription.IsListening);
        Assert.Equal(1, Events.Publish(audited.Raised(EventOutcome.Timeout)));

        EventRecord raised = await heard.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(EventOutcome.Timeout, raised.Outcome);
        Assert.Null(subscription.Fault);
        Assert.Throws<InvalidOperationException>(
            () => subscription.Next(TimeSpan.Zero, 1));
    }

    [Fact]
    public async Task WhatAListenerThrowsStaysOnThisSideAndIsKept()
    {
        using Audited audited = new();
        TaskCompletionSource called = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using EventSubscription subscription = Events.Listen(
            "Xmip.Abi.Tests", audited.Directory, Party,
            _ =>
            {
                called.TrySetResult();
                throw new InvalidOperationException("thrown by the test's listener");
            },
            new EventFilter { Scope = audited.Scope });

        Events.Publish(audited.Raised(EventOutcome.Failure));
        await called.Task.WaitAsync(TimeSpan.FromSeconds(2));

        // The fault is kept after the call returns; give it that moment.
        for (int i = 0; i < 200 && subscription.Fault is null; i++)
        {
            await Task.Delay(1);
        }

        Assert.IsType<InvalidOperationException>(subscription.Fault);
    }

    [Fact]
    public void WhatTheRuntimeRefusesIsRefused()
    {
        using Audited audited = new();

        Assert.Throws<ArgumentException>(
            () => Events.Subscribe("Xmip.Abi.Tests", audited.Directory, string.Empty));
        Assert.Throws<ArgumentException>(
            () => Events.Subscribe("Xmip.Abi.Tests", audited.Directory, "not a uuid"));
        Assert.Throws<ArgumentException>(
            () => Events.Publish(new EventRecord { Type = "t", Scope = string.Empty }));
        Assert.Throws<ArgumentException>(() => Events.Publish(
            audited.Raised(EventOutcome.Failure) with { Journey = "not a uuid" }));
        Assert.Throws<ArgumentException>(() => Events.Publish(
            audited.Raised(EventOutcome.Failure) with { Outcome = (EventOutcome)8 }));
    }

    [Fact]
    public void ADisposedSubscriptionIsNotDrained()
    {
        using Audited audited = new();
        EventSubscription subscription = Events.Subscribe(
            "Xmip.Abi.Tests", audited.Directory, Party, new EventFilter { Scope = audited.Scope });

        subscription.Dispose();

        Assert.Equal(0, Events.Publish(audited.Raised(EventOutcome.Success)));
        Assert.Throws<ObjectDisposedException>(() => subscription.Next(TimeSpan.Zero, 1));
    }
}

/// <summary>A test's own scope, and its own audit directory, removed
/// afterwards.</summary>
internal sealed class Audited : IDisposable
{
    public string Directory { get; } =
        Path.Combine(Path.GetTempPath(), $"xmip-abi-event-{Guid.NewGuid():n}");

    public string Scope { get; } = $"xmip:///abi-event-{Guid.NewGuid():n}";

    /// <summary>An Event at this test's scope, ending in
    /// <paramref name="outcome"/>.</summary>
    public EventRecord Raised(EventOutcome outcome, string? sent = null)
    {
        Dictionary<string, string> diagnostics = [];

        if (sent is not null)
        {
            diagnostics["sent"] = sent;
        }

        return new EventRecord
        {
            Type = "se.xmip.process.test",
            Action = EventAction.Process,
            Outcome = outcome,
            Scope = Scope + "/node/n",
            Diagnostics = diagnostics,
        };
    }

    public void Dispose()
    {
        // The audit is written off the subscriber's path and may still be
        // closing the file; what it leaves is the temporary directory's.
        try
        {
            System.IO.Directory.Delete(Directory, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
