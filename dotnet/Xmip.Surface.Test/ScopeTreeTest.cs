using Xmip.Abi.Operate;

namespace Xmip.Surface.Test;

/// <summary>
/// The scope tree is one tree (ADR-0027 clause 4) with one rollup (ADR-0041)
/// and one worst leaf (ADR-0052 clause 2). Every surface reads these, so a
/// defect here would be every surface's defect at once.
/// </summary>
public sealed class ScopeTreeTest
{
    private static readonly DateTimeOffset Now = new(2026, 9, 11, 12, 0, 0, TimeSpan.Zero);

    private static readonly HealthRecord[] Leaves =
    [
        new("xmip:///edge-01/receive/orders", HealthState.Fine, 0, "", Now),
        new("xmip:///edge-01/receive/partner", HealthState.Done, 95, "refused", Now.AddSeconds(-4)),
        new("xmip:///edge-01/process/approval", HealthState.Stressed, 55, "waiting", Now),
        new("xmip:///edge-02/send/warehouse", HealthState.Paused, 30, "paused by ilian", Now),
        new("xmip:///edge-02/send/billing", HealthState.Fine, 0, "", Now),
    ];

    [Fact]
    public void PartsDropTheSchemeAndAnEmptyAuthority()
    {
        Assert.Equal(
            ["edge-01", "receive", "orders"],
            ScopeTree.Parts("xmip:///edge-01/receive/orders"));
        Assert.Empty(ScopeTree.Parts(ScopeTree.Root));
        Assert.Empty(ScopeTree.Parts("xmip://"));
    }

    [Fact]
    public void PartsDropAHostToo()
    {
        Assert.Equal(["edge-01", "receive"], ScopeTree.Parts("xmip://lab:9000/edge-01/receive/"));
    }

    [Fact]
    public void EverythingIsBeneathTheRoot()
    {
        Assert.All(Leaves, leaf => Assert.True(ScopeTree.Beneath(leaf.Scope, ScopeTree.Root)));
    }

    [Fact]
    public void BeneathIsBySegmentNotByPrefix()
    {
        Assert.True(ScopeTree.Beneath("xmip:///edge-01/receive/orders", "xmip:///edge-01"));
        Assert.True(ScopeTree.Beneath("xmip:///edge-01", "xmip:///edge-01/"));
        Assert.False(ScopeTree.Beneath("xmip:///edge-010/receive", "xmip:///edge-01"));
        Assert.False(ScopeTree.Beneath("xmip:///edge-01", "xmip:///edge-01/receive"));
    }

    [Fact]
    public void ParentClimbsOneLevelAndStopsAtTheRoot()
    {
        Assert.Equal("xmip:///edge-01/receive", ScopeTree.Parent("xmip:///edge-01/receive/orders"));
        Assert.Equal(ScopeTree.Root, ScopeTree.Parent("xmip:///edge-01"));
        Assert.Equal(ScopeTree.Root, ScopeTree.Parent(ScopeTree.Root));
    }

    [Fact]
    public void NodeSegmentAndNameReadTheThreeLevels()
    {
        const string scope = "xmip:///edge-01/receive/orders/in";

        Assert.Equal("edge-01", ScopeTree.Node(scope));
        Assert.Equal("receive", ScopeTree.Segment(scope, 1));
        Assert.Equal("orders/in", ScopeTree.Name(scope));
        Assert.Equal("receive", ScopeTree.Name("xmip:///edge-01/receive"));
        Assert.Equal("", ScopeTree.Node(ScopeTree.Root));
    }

    [Fact]
    public void AParentIsOnlyEverFineOrHolding()
    {
        Assert.Equal(HealthState.Fine, ScopeTree.Rolled(HealthState.Fine));

        IEnumerable<HealthState> notFine =
            Enum.GetValues<HealthState>().Where(mood => mood != HealthState.Fine);

        foreach (HealthState mood in notFine)
        {
            Assert.Equal(HealthState.Holding, ScopeTree.Rolled(mood));
        }
    }

    [Fact]
    public void TheRollupOverLeavesIsHoldingWhenOneIsNotFine()
    {
        Assert.Equal(HealthState.Holding, ScopeTree.Rollup(Leaves));
        Assert.Equal(
            HealthState.Fine,
            ScopeTree.Rollup(Leaves.Where(leaf => leaf.State == HealthState.Fine)));
        Assert.Null(ScopeTree.Rollup([]));
    }

    [Fact]
    public void TheWorstLeafIsTheWorstMoodThenTheHighestSeverity()
    {
        HealthRecord? worst = ScopeTree.Worst(Leaves);

        Assert.NotNull(worst);
        Assert.Equal("xmip:///edge-01/receive/partner", worst.Scope);
        Assert.Equal("refused", worst.Evidence);
        Assert.Equal(Now.AddSeconds(-4), worst.Observed);
    }

    [Fact]
    public void TheWorstLeafBeneathAScopeIgnoresWhatIsOutsideIt()
    {
        HealthRecord? worst = ScopeTree.WorstLeaf(Leaves, "xmip:///edge-02");

        Assert.NotNull(worst);
        Assert.Equal(HealthState.Paused, worst.State);
        Assert.Equal("paused by ilian", worst.Evidence);
        Assert.Null(ScopeTree.WorstLeaf(Leaves, "xmip:///edge-03"));
    }

    [Fact]
    public void EqualsAnswerTheSameWayEveryTime()
    {
        HealthRecord[] equal =
        [
            new("xmip:///n/send/b", HealthState.Done, 50, "b", Now),
            new("xmip:///n/send/a", HealthState.Done, 50, "a", Now),
        ];

        Assert.Equal("xmip:///n/send/a", ScopeTree.Worst(equal)!.Scope);
        Assert.Equal("xmip:///n/send/a", ScopeTree.Worst(equal.Reverse())!.Scope);
    }

    [Fact]
    public void BranchesBeneathTheRootAreTheNodesRolledUp()
    {
        IReadOnlyList<Branch> branches = ScopeTree.Branches(Leaves, ScopeTree.Root);

        Assert.Equal(2, branches.Count);
        Assert.Equal("edge-01", branches[0].Label);
        Assert.Equal(HealthState.Holding, branches[0].State);
        Assert.False(branches[0].IsLeaf);
        Assert.Equal(3, branches[0].Count);
        Assert.Equal("refused", branches[0].Worst.Evidence);
        Assert.Equal("edge-02", branches[1].Label);
        Assert.Equal("paused by ilian", branches[1].Worst.Evidence);
    }

    [Fact]
    public void BranchesAtTheBottomAreLeavesWithTheirOwnMood()
    {
        IReadOnlyList<Branch> branches = ScopeTree.Branches(Leaves, "xmip:///edge-01/receive");

        Assert.Equal(2, branches.Count);
        Assert.True(branches[0].IsLeaf);
        Assert.Equal(HealthState.Done, branches[0].State);
        Assert.Equal("partner", branches[0].Label);
        Assert.Equal(HealthState.Fine, branches[1].State);
    }

    [Fact]
    public void TheTrailIsEveryStepBackToTheCluster()
    {
        IReadOnlyList<Crumb> trail = ScopeTree.Trail("xmip:///edge-01/receive");

        Assert.Equal(
            [new Crumb("cluster", ScopeTree.Root),
             new Crumb("edge-01", "xmip:///edge-01"),
             new Crumb("receive", "xmip:///edge-01/receive")],
            trail);
    }
}
