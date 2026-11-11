using System.Runtime.InteropServices;
using Xmip.Abi.Operate;

namespace Xmip.Abi.Test;

/// <summary>
/// <see cref="RuntimeEvents"/> crosses section 11 of <c>xmip_operate.h</c> to
/// the runtime this estate built and back. A program subscribing names no
/// Party of its own: it is the account this process runs as, resolved
/// through the Parties of the node serving in the process (ADR-0019,
/// amendment 2026-11-11). No node serves in a test's process, so these prove
/// a subscription refused with the sentence, whatever the policy allows,
/// and an Event published, the policy and who is unheard crossing whole.
/// Draining and being called back are proven where a node serves, in the
/// runtime's own tests. Every refusal audits into a temporary directory of
/// the test's own, never the operating system's log.
/// </summary>
public sealed class RuntimeEventsTest
{
    /// <summary>What the sentence says of an account no Party holds.</summary>
    internal const string NoParty = "is no Party of a node serving in this process";

    /// <summary>The policy the tests hand the hub: every Party. A subscriber
    /// still has to prove to be one.</summary>
    internal static Func<EventAuthorization, bool?> Allowed { get; } = _ => true;

    private static RuntimeEvents Events => RuntimeRulesTest.Rules.Events;

    [Fact]
    public void TheShapesAreTheHeadersOnASixtyFourBitProcess()
    {
        Assert.True(Environment.Is64BitProcess, "the estate builds 64-bit");

        // Thirteen strings of 16 bytes, a time, two enums, a pointer and a length.
        Assert.Equal(192, Marshal.SizeOf<XmipEvent>());
        Assert.Equal(64, Marshal.SizeOf<XmipEventFilter>());
    }

    [Fact]
    public void AProgramSubscribingNamesNoPartyOfItsOwn()
    {
        using Audited audited = new();
        EventFilter here = new() { Scope = audited.Scope };

        UnauthorizedAccessException refused = Assert.Throws<UnauthorizedAccessException>(
            () => Events.Subscribe("Xmip.Abi.Test", audited.Directory, here));
        Assert.Contains(NoParty, refused.Message, StringComparison.Ordinal);
        Assert.StartsWith("REFUSED: ", refused.Message, StringComparison.Ordinal);

        refused = Assert.Throws<UnauthorizedAccessException>(
            () => Events.Listen("Xmip.Abi.Test", audited.Directory, _ => { }, here));
        Assert.Contains(NoParty, refused.Message, StringComparison.Ordinal);

        // The refusal is in the program's audit, as every refusal is.
        string audit = File.ReadAllText(Path.Combine(audited.Directory, "audit.toml"));
        Assert.Contains("action = \"event.subscribe\"", audit, StringComparison.Ordinal);
        Assert.Contains(NoParty, audit, StringComparison.Ordinal);
    }

    [Fact]
    public void AnEventPublishedHereCrossesWholeAndNobodyUnheardIsSaidSo()
    {
        using Audited audited = new();

        int delivered = Events.Publish(new EventRecord
        {
            Type = "se.xmip.receive.failure",
            Action = EventAction.Receive,
            Outcome = EventOutcome.Failure,
            Scope = audited.Node + "/receive/orders",
            Journey = "0198a3c4-0000-7000-8000-000000000001",
            Endpoint = "tcp://0.0.0.0:5000",
            Artifact = "orders",
            Diagnostics = new Dictionary<string, string> { ["status"] = "refused", ["ö"] = "å" },
        });

        Assert.Equal(0, delivered);
        Assert.Empty(Events.Unheard());
    }

    [Fact]
    public void WhatTheRuntimeRefusesIsRefused()
    {
        using Audited audited = new();

        Assert.Throws<ArgumentException>(() => Events.Subscribe(
            "Xmip.Abi.Test", audited.Directory, new EventFilter { Party = "not a uuid" }));
        Assert.Throws<ArgumentException>(
            () => Events.Publish(new EventRecord { Type = "t", Scope = string.Empty }));
        Assert.Throws<ArgumentException>(() => Events.Publish(
            audited.Raised(EventOutcome.Failure) with { Journey = "not a uuid" }));
        Assert.Throws<ArgumentException>(() => Events.Publish(
            audited.Raised(EventOutcome.Failure) with { Outcome = (EventOutcome)8 }));
    }
}

/// <summary>A test's own scope, and its own audit directory, removed
/// afterwards.</summary>
internal sealed class Audited : IDisposable
{
    public string Directory { get; } =
        Path.Combine(Path.GetTempPath(), $"xmip-abi-event-{Guid.NewGuid():n}");

    private static readonly TestCluster Cluster = TestCluster.Read();

    public string Scope { get; } = $"{Cluster.Scope}/abi-event-{Guid.NewGuid():n}";

    /// <summary>The test cluster's receiving node beneath this test's scope.</summary>
    public string Node => $"{Scope}/node/{Cluster.WithRole("receiving")}";

    /// <summary>An Event at this test's scope, ending in
    /// <paramref name="outcome"/>.</summary>
    public EventRecord Raised(EventOutcome outcome)
    {
        return new EventRecord
        {
            Type = "se.xmip.process.test",
            Action = EventAction.Process,
            Outcome = outcome,
            Scope = Node,
        };
    }

    public void Dispose()
    {
        // The audit may still be closing the file; what it leaves is the
        // temporary directory's.
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
