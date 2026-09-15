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

    private static readonly HealthRecord[] Leaves =
    [
        Leaf("xmip:///edge-01/receive/orders", HealthState.Fine),
        Leaf("xmip:///edge-01/receive/partner", HealthState.Done, 95, "refused"),
        Leaf("xmip:///edge-01/process/approval", HealthState.Stressed, 55, "waiting"),
        Leaf("xmip:///edge-02/send/warehouse", HealthState.Paused, 30, "paused by ilian"),
        Leaf("xmip:///edge-02/send/billing", HealthState.Fine),
    ];

    private static readonly ScopeIndex.Count[] Counts =
    [
        new("xmip:///edge-01/receive", Counted.Streams, 3, Seen),
        new("xmip:///edge-02/send", Counted.Messages, 7, Seen),
        new("xmip:///edge-01/receive", Counted.Failed, 1, null),
    ];

    [Fact]
    public void BranchesAreWhatTheHelpersSay()
    {
        ScopeIndex index = ScopeIndex.Build(Leaves, Counts, 1, "test");

        Assert.Equal(ScopeTree.Branches(Leaves, ScopeTree.Root), index.Branches(ScopeTree.Root));
        Assert.Equal(
            ScopeTree.Branches(Leaves, "xmip:///edge-01/receive"),
            index.Branches("xmip:///edge-01/receive"));
        Assert.Empty(index.Branches("xmip:///nowhere"));
    }

    [Fact]
    public void HealthIsWorstFirstAndOnlyWhatIsBeneath()
    {
        ScopeIndex index = ScopeIndex.Build(Leaves, Counts, 1, "test");

        Assert.Equal(ScopeTree.WorstFirst(Leaves), index.Health(ScopeTree.Root));
        Assert.Equal(
            ["xmip:///edge-02/send/warehouse", "xmip:///edge-02/send/billing"],
            index.Health("xmip:///edge-02").Select(record => record.Scope));
        Assert.Equal("refused", index.Worst(ScopeTree.Root)!.Evidence);
        Assert.Equal(5, index.Leaves);
        Assert.Equal(["edge-01", "edge-02"], index.Nodes());
    }

    [Fact]
    public void AParentHoldsAndALeafKeepsItsOwnMood()
    {
        ScopeIndex index = ScopeIndex.Build(Leaves, Counts, 1, "test");

        Assert.Equal(HealthState.Holding, index.Rollup(ScopeTree.Root));
        Assert.Equal(HealthState.Holding, index.Rollup("xmip:///edge-01"));
        Assert.Equal(HealthState.Done, index.Rollup("xmip:///edge-01/receive/partner"));
        Assert.Equal(HealthState.Fine, index.Rollup("xmip:///edge-02/send/billing"));
        Assert.Null(index.Rollup("xmip:///nowhere"));
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

        Assert.Equal(3UL, index.Figures("xmip:///edge-01").Streams);
        Assert.Null(index.Figures("xmip:///edge-01").Messages);
        Assert.Null(index.Measure("xmip:///edge-02", Counted.Streams));
        Assert.Equal(7UL, index.Measure("xmip:///edge-02/send", Counted.Messages)!.Value);
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
                        $"xmip:///C1/node/N{node}/{stage}/transport-{pair % 40}/contract-{pair}",
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
        IReadOnlyList<Branch> nodes = index.Branches("xmip:///C1/node");
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
