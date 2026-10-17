using System.Diagnostics;
using System.Globalization;
using Xmip.Abi.Operate;
using Xunit.Abstractions;

namespace Xmip.Abi.Test;

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
/// Two things are held, each by what is Xmip's alone. <b>The wake is a
/// wake</b>: the median from publish to the copy in hand is under a
/// millisecond plus the median of what the operating system takes to wake a
/// thread on a plain kernel event, measured beside it round by round. A wake
/// that is broken — a poll, a missed notify, a timeout waited out — puts most
/// rounds at the poll interval and fails it; one that never comes fails the
/// five seconds each round waits. <b>Xmip's own work is under a
/// millisecond, every Event</b>: the 99th percentile of the processor time
/// the publishing thread spends in <c>Publish</c> and the subscriber's
/// thread spends between one Event and the next — the wait entered, the
/// wake taken, the drain, the crossing and the copy — counted by
/// <see cref="ProcessorTime"/>, which counts only while a thread runs. The
/// rule is a millisecond <em>apart from load</em>, and that is the time
/// load cannot reach. What the wall clock's tail is, is printed and never
/// asserted: on a machine busy on every processor a woken thread waits its
/// turn for one, up to a scheduler's quantum of about fifteen milliseconds,
/// and a plain wake measured a moment apart meets another turn — under
/// sixteen busy processes the plain wake's 99th percentile ran from 0.1 to
/// 14 milliseconds and the Event's from 2 to 21, neither following the
/// other, so a tail held against it failed seven runs in eight and measured
/// the scheduler.
/// </remarks>
[Collection(nameof(EventLatency))]
public sealed class EventLatencyTest(ITestOutputHelper output)
{
    private const int Warm = 50;
    private const int Measured = 500;
    private const double Bound = 1.0;

    private static RuntimeEvents Events => RuntimeRulesTest.Rules.Events;

    [Fact]
    public void ADrainingSubscriberWakesWithinAMillisecondOfPublish()
    {
        using Audited audited = new();
        using EventSubscription subscription = Events.Subscribe(
            "Xmip.Abi.Test", audited.Directory, RuntimeEventsTest.Party,
            new EventFilter { Scope = audited.Scope });
        List<double> took = [];
        List<long> worked = [];
        using AutoResetEvent received = new(false);
        Exception? failed = null;

        Thread drainer = new(() =>
        {
            try
            {
                for (int i = 0; i < Warm + Measured; i++)
                {
                    long from = ProcessorTime.Thread();
                    EventRecord heard = Assert.Single(
                        subscription.Next(TimeSpan.FromSeconds(5), 1).Events);

                    worked.Add(ProcessorTime.Thread() - from);
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
        (List<double> plain, List<long> published) = Publish(audited, received);
        drainer.Join();

        Assert.Null(failed);
        Hold("publish to Next", took, plain, Own(published, worked));
    }

    [Fact]
    public void AListeningSubscriberIsCalledWithinAMillisecondOfPublish()
    {
        using Audited audited = new();
        List<double> took = [];
        List<long> worked = [];
        long left = 0;
        using AutoResetEvent received = new(false);
        using EventSubscription subscription = Events.Listen(
            "Xmip.Abi.Test", audited.Directory, RuntimeEventsTest.Party,
            heard =>
            {
                // The runtime's thread between the last callback and this
                // one: the wait entered, the wake, the drain, the crossing
                // and the copy. The first has no last, and is warm-up.
                long now = ProcessorTime.Thread();

                worked.Add(left == 0 ? 0 : now - left);
                took.Add(Since(heard));
                received.Set();
                left = ProcessorTime.Thread();
            },
            new EventFilter { Scope = audited.Scope });

        (List<double> plain, List<long> published) = Publish(audited, received);

        Assert.Null(subscription.Fault);
        Hold("publish to callback", took, plain, Own(published, worked));
    }

    // Each round the publisher stamps an Event as it hands it over and waits
    // for it to arrive, so every Event meets an idle subscriber; then it
    // wakes the plain thread the same way. What the plain thread took, in
    // milliseconds, and the publishing thread's own time in Publish, are
    // returned.
    private static (List<double> Plain, List<long> Published) Publish(
        Audited audited, AutoResetEvent received)
    {
        using AutoResetEvent told = new(false);
        using AutoResetEvent woke = new(false);
        long stamp = 0;
        bool done = false;
        List<double> plain = [];
        List<long> published = [];

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
            EventRecord raised = audited.Raised(EventOutcome.Success, sent);
            long from = ProcessorTime.Thread();

            Assert.Equal(1, Events.Publish(raised));
            published.Add(ProcessorTime.Thread() - from);
            Assert.True(received.WaitOne(TimeSpan.FromSeconds(5)), $"Event {i} did not arrive");

            Volatile.Write(ref stamp, Stopwatch.GetTimestamp());
            told.Set();
            Assert.True(woke.WaitOne(TimeSpan.FromSeconds(5)), $"the plain wake {i} did not");
        }

        Volatile.Write(ref done, true);
        told.Set();
        control.Join();

        return (plain, published);
    }

    // Milliseconds from the stamp to the managed copy in hand.
    private static double Since(EventRecord heard)
    {
        long sent = long.Parse(heard.Diagnostics["sent"], CultureInfo.InvariantCulture);

        return Stopwatch.GetElapsedTime(sent).TotalMilliseconds;
    }

    // Xmip's own processor time for each Event, in milliseconds: the
    // publisher's in Publish and the subscriber's thread's, round by round.
    private static List<double> Own(List<long> published, List<long> worked)
    {
        Assert.Equal(published.Count, worked.Count);

        return [.. published.Zip(worked, (one, other) => ProcessorTime.Milliseconds(one + other))];
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

    private void Hold(string what, List<double> took, List<double> plain, List<double> own)
    {
        (double median, _) = Spread(what, took);
        (double usual, _) = Spread("a plain wake beside it", plain);
        (_, double p99) = Spread("Xmip's own processor time", own);

        Assert.True(
            median < Bound + usual,
            $"{what}: median {median:0.000} ms, the machine's own {usual:0.000} ms");
        Assert.True(
            p99 < Bound,
            $"{what}: Xmip's own processor time, p99 {p99:0.000} ms, over {Bound} ms");
    }
}
