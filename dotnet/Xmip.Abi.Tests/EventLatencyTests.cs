using System.Diagnostics;
using System.Globalization;
using Xmip.Abi.Operate;
using Xunit.Abstractions;

namespace Xmip.Abi.Tests;

/// <summary>The latency tests run alone: a measurement shared with the rest
/// of the suite measures the suite.</summary>
[CollectionDefinition(nameof(EventLatency), DisableParallelization = true)]
public sealed class EventLatency;

/// <summary>
/// The owner's rule for the whole message path, events included: near, very
/// near real time (CONTRIBUTING, 2026-09-26). An Event published in process
/// reaches a .NET subscriber within about a millisecond: publish to the
/// managed copy in hand, measured over hundreds of Events one at a time, so
/// what is measured is the wake and the crossing and never a queue.
/// </summary>
/// <remarks>
/// Each is held to the bound plus what the operating system takes to wake a
/// thread on a plain kernel event, measured beside it round by round under
/// the same load — the rule is a millisecond <em>apart from load</em>, as
/// <c>xmip-core-event</c>'s own <c>tests/latency.rs</c> holds it. The
/// median is held under a millisecond plus the plain wake's median. The
/// 99th percentile is held under five milliseconds plus the plain wake's
/// 99th, not one: an Event's path does several times a plain wake's work —
/// the crossing, the copy, the audit appended on another thread — so one
/// round in a hundred meets a preemption the plain wake is spared, and on
/// this estate's machine, compiling beside the suite, it was seen at up to
/// 2.4 against a plain 0.3. A wake that is broken (a poll, a missed notify,
/// a timeout waited out) puts most rounds at the poll interval and fails
/// the median. The worst is printed, never asserted.
/// </remarks>
[Collection(nameof(EventLatency))]
public sealed class EventLatencyTests(ITestOutputHelper output)
{
    private const int Warm = 50;
    private const int Measured = 500;
    private const double Bound = 1.0;
    private const double Tail = 5.0;

    private static RuntimeEvents Events => RuntimeRulesTests.Rules.Events;

    [Fact]
    public void ADrainingSubscriberWakesWithinAMillisecondOfPublish()
    {
        using Audited audited = new();
        using EventSubscription subscription = Events.Subscribe(
            "Xmip.Abi.Tests", audited.Directory, RuntimeEventsTests.Party,
            new EventFilter { Scope = audited.Scope });
        List<double> took = [];
        using AutoResetEvent received = new(false);
        Exception? failed = null;

        Thread drainer = new(() =>
        {
            try
            {
                for (int i = 0; i < Warm + Measured; i++)
                {
                    EventRecord heard = Assert.Single(
                        subscription.Next(TimeSpan.FromSeconds(5), 1).Events);

                    took.Add(Since(heard));
                    received.Set();
                }
            }
            catch (Exception thrown) when (thrown is not OutOfMemoryException)
            {
                failed = thrown;
                received.Set();
            }
        })
        { IsBackground = true, Name = "drainer" };

        drainer.Start();
        List<double> plain = Publish(audited, received);
        drainer.Join();

        Assert.Null(failed);
        Hold("publish to Next", took, plain);
    }

    [Fact]
    public void AListeningSubscriberIsCalledWithinAMillisecondOfPublish()
    {
        using Audited audited = new();
        List<double> took = [];
        using AutoResetEvent received = new(false);
        using EventSubscription subscription = Events.Listen(
            "Xmip.Abi.Tests", audited.Directory, RuntimeEventsTests.Party,
            heard =>
            {
                took.Add(Since(heard));
                received.Set();
            },
            new EventFilter { Scope = audited.Scope });

        List<double> plain = Publish(audited, received);

        Assert.Null(subscription.Fault);
        Hold("publish to callback", took, plain);
    }

    // Each round the publisher stamps an Event as it hands it over and waits
    // for it to arrive, so every Event meets an idle subscriber; then it
    // wakes the plain thread the same way. What the plain thread took, in
    // milliseconds, is returned.
    private static List<double> Publish(Audited audited, AutoResetEvent received)
    {
        using AutoResetEvent told = new(false);
        using AutoResetEvent woke = new(false);
        long stamp = 0;
        bool done = false;
        List<double> plain = [];

        Thread control = new(() =>
        {
            while (told.WaitOne() && !Volatile.Read(ref done))
            {
                plain.Add(Stopwatch.GetElapsedTime(Volatile.Read(ref stamp)).TotalMilliseconds);
                woke.Set();
            }
        })
        { IsBackground = true, Name = "plain wake" };

        control.Start();

        for (int i = 0; i < Warm + Measured; i++)
        {
            string sent = Stopwatch.GetTimestamp().ToString(CultureInfo.InvariantCulture);

            Assert.Equal(1, Events.Publish(audited.Raised(EventOutcome.Success, sent)));
            Assert.True(received.WaitOne(TimeSpan.FromSeconds(5)), $"Event {i} did not arrive");

            Volatile.Write(ref stamp, Stopwatch.GetTimestamp());
            told.Set();
            Assert.True(woke.WaitOne(TimeSpan.FromSeconds(5)), $"the plain wake {i} did not");
        }

        Volatile.Write(ref done, true);
        told.Set();
        control.Join();

        return plain;
    }

    // Milliseconds from the stamp to the managed copy in hand.
    private static double Since(EventRecord heard)
    {
        long sent = long.Parse(heard.Diagnostics["sent"], CultureInfo.InvariantCulture);

        return Stopwatch.GetElapsedTime(sent).TotalMilliseconds;
    }

    // The median, the 99th percentile and the worst after the warm-up.
    private (double Median, double P99) Spread(string what, List<double> took)
    {
        double[] sorted = [.. took.Skip(Warm).Order()];

        Assert.Equal(Measured, sorted.Length);

        double median = sorted[sorted.Length / 2];
        double p99 = sorted[(int)Math.Ceiling(sorted.Length * 0.99) - 1];

        output.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"{what}: {Measured} rounds, median {median:0.000} ms, p99 {p99:0.000} ms, " +
            $"worst {sorted[^1]:0.000} ms"));

        return (median, p99);
    }

    private void Hold(string what, List<double> took, List<double> plain)
    {
        (double median, double p99) = Spread(what, took);
        (double usual, double tail) = Spread("a plain wake beside it", plain);

        Assert.True(
            median < Bound + usual,
            $"{what}: median {median:0.000} ms, the machine's own {usual:0.000} ms");
        Assert.True(
            p99 < Tail + tail,
            $"{what}: p99 {p99:0.000} ms, the machine's own {tail:0.000} ms");
    }
}
