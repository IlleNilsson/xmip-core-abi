using System.Diagnostics;
using Xmip.Abi.Operate;

namespace Xmip.Surface.Test;

/// <summary>
/// The publication as its tree, built once (ADR-0052, amendment 2026-09-15):
/// what it answers is what the record-by-record helpers answer, and it
/// answers a Playground's worth of leaves in the time one render can afford.
/// </summary>
public sealed class ScopeIndexTest
{
    private static readonly DateTimeOffset Seen = new(2026, 9, 15, 8, 0, 0, TimeSpan.Zero);

    private static readonly TestCluster Cluster = TestCluster.Read();

    // Nodes of the test cluster by what each declares, each directly beneath
    // the root as in a node's own publication; the processing node published
    // nothing here.
    private static readonly string Receiver = Cluster.WithRole("receiving");
    private static readonly string Sender = Cluster.WithRole("sending");
    private static readonly string Received = ScopeTree.Root + Receiver;
    private static readonly string Sent = ScopeTree.Root + Sender;
    private static readonly string Silent = ScopeTree.Root + Cluster.WithRole("processing");

    private static readonly HealthRecord[] Leaves =
    [
        Leaf($"{Received}/receive/orders", HealthState.Fine),
        Leaf($"{Received}/receive/party", HealthState.Done, 95, "refused"),
        Leaf($"{Received}/process/approval", HealthState.Stressed, 55, "waiting"),
        Leaf($"{Sent}/send/warehouse", HealthState.Paused, 30, "paused by ilian"),
        Leaf($"{Sent}/send/billing", HealthState.Fine),
    ];

    private static readonly ScopeIndex.Count[] Counts =
    [
        new($"{Received}/receive", Counted.Streams, 3, Seen),
        new($"{Sent}/send", Counted.Messages, 7, Seen),
        new($"{Received}/receive", Counted.Failed, 1, null),
    ];

    [Fact]
    public void BranchesAreWhatTheHelpersSay()
    {
        ScopeIndex index = ScopeIndex.Build(Leaves, Counts, 1, "test");

        Assert.Equal(ScopeTree.Branches(Leaves, ScopeTree.Root), index.Branches(ScopeTree.Root));
        Assert.Equal(
            ScopeTree.Branches(Leaves, $"{Received}/receive"),
            index.Branches($"{Received}/receive"));
        Assert.Empty(index.Branches(Silent));
    }

    [Fact]
    public void HealthIsWorstFirstAndOnlyWhatIsBeneath()
    {
        ScopeIndex index = ScopeIndex.Build(Leaves, Counts, 1, "test");

        Assert.Equal(ScopeTree.WorstFirst(Leaves), index.Health(ScopeTree.Root));
        Assert.Equal(
            [$"{Sent}/send/warehouse", $"{Sent}/send/billing"],
            index.Health(Sent).Select(record => record.Scope));
        Assert.Equal("refused", index.Worst(ScopeTree.Root)!.Evidence);
        Assert.Equal(5, index.Leaves);
        Assert.Equal(
            new[] { Receiver, Sender }.Order(StringComparer.Ordinal), index.Nodes());
    }

    [Fact]
    public void AParentHoldsAndALeafKeepsItsOwnMood()
    {
        ScopeIndex index = ScopeIndex.Build(Leaves, Counts, 1, "test");

        Assert.Equal(HealthState.Holding, index.Rollup(ScopeTree.Root));
        Assert.Equal(HealthState.Holding, index.Rollup(Received));
        Assert.Equal(HealthState.Done, index.Rollup($"{Received}/receive/party"));
        Assert.Equal(HealthState.Fine, index.Rollup($"{Sent}/send/billing"));
        Assert.Null(index.Rollup(Silent));
    }

    [Fact]
    public void FiguresSumUpwardAndAnUnpublishedFigureIsAbsent()
    {
        ScopeIndex index = ScopeIndex.Build(Leaves, Counts, 1, "test");

        Figures root = index.Figures(ScopeTree.Root);
        Assert.Equal(3UL, root.Streams);
        Assert.Equal(7UL, root.Messages);
        Assert.Equal(1UL, root.Failed);
        Assert.Null(root.Journeys);
        Assert.Equal(Seen, root.Observed);

        Assert.Equal(3UL, index.Figures(Received).Streams);
        Assert.Null(index.Figures(Received).Messages);
        Assert.Null(index.Measure(Sent, Counted.Streams));
        Assert.Equal(7UL, index.Measure($"{Sent}/send", Counted.Messages)!.Value);
    }

    [Fact]
    public void APlaygroundOfLeavesIsIndexedWithinOneRender()
    {
        // Eleven thousand leaves is what a roll publishes every second; the
        // board must read it in a fraction of that (2026-09-15).
        List<HealthRecord> many = [];

        for (int node = 0; node < 12; node++)
        {
            foreach (string stage in ScopeTree.Stages)
            {
                for (int pair = 0; pair < 320; pair++)
                {
                    many.Add(Leaf(
                        $"{Cluster.Scope}/node/{Receiver}-{node}/{stage}/transport-{pair % 40}"
                        + $"/contract-{pair}",
                        pair % 97 == 0 ? HealthState.Stressed : HealthState.Fine,
                        (byte)(pair % 97 == 0 ? 55 : 0)));
                }
            }
        }

        // Once to warm the code, then the measured build; the suite runs its
        // classes in parallel, so the bound is generous and still a fraction
        // of the second between publications.
        _ = ScopeIndex.Build(many, [], 0, "warm");
        Stopwatch clock = Stopwatch.StartNew();
        ScopeIndex index = ScopeIndex.Build(many, [], 1, "test");
        IReadOnlyList<Branch> nodes = index.Branches($"{Cluster.Scope}/node");
        IReadOnlyList<HealthRecord> all = index.Health(ScopeTree.Root);
        clock.Stop();

        Assert.Equal(many.Count, index.Leaves);
        Assert.Equal(12, nodes.Count);
        Assert.Equal(HealthState.Stressed, all[0].State);
        Assert.True(clock.ElapsedMilliseconds < 2_000, $"took {clock.ElapsedMilliseconds} ms");
    }

    private static HealthRecord Leaf(
        string scope, HealthState state, byte severity = 0, string evidence = "")
    {
        return new HealthRecord(scope, state, severity, evidence, Seen);
    }
}
