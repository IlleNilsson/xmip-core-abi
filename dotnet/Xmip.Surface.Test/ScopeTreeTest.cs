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

    private static readonly TestCluster Cluster = TestCluster.Read();

    // Nodes of the test cluster by what each declares, and each as a scope
    // directly beneath the root, the shape of a node's own publication.
    private static readonly string Receiver = Cluster.WithRole("receiving");
    private static readonly string Sender = Cluster.WithRole("sending");
    private static readonly string Processor = Cluster.WithRole("processing");
    private static readonly string Received = ScopeTree.Root + Receiver;
    private static readonly string Sent = ScopeTree.Root + Sender;

    private static readonly HealthRecord[] Leaves =
    [
        new($"{Received}/receive/orders", HealthState.Fine, 0, "", Now),
        new($"{Received}/receive/party", HealthState.Done, 95, "refused", Now.AddSeconds(-4)),
        new($"{Received}/process/approval", HealthState.Stressed, 55, "waiting", Now),
        new($"{Sent}/send/warehouse", HealthState.Paused, 30, "paused by ilian", Now),
        new($"{Sent}/send/billing", HealthState.Fine, 0, "", Now),
    ];

    /// <summary>Candidates and scopes the export is asked about: beneath, a
    /// sibling whose name the scope's is a prefix of, above, one with an
    /// authority, a scheme in capitals, and nothing.</summary>
    public static TheoryData<string, string> Containment => new()
    {
        { $"{Received}/receive/orders", Received },
        { $"{Received}0/receive", Received },
        { Received, $"{Received}/receive" },
        { $"xmip://localhost:9000/{Receiver}/receive/", ScopeTree.Root },
        { Cluster.Scope.ToUpperInvariant(), Cluster.Scope },
        { "", "" },
    };

    /// <summary>Scopes and their names within the cluster.</summary>
    public static TheoryData<string, string> Names => new()
    {
        { $"{Cluster.Scope}/filing/s3/xml", "filing/s3/xml" },
        {
            $"{Cluster.Scope}/node/{Receiver}/receive/http/json",
            $"node/{Receiver}/receive/http/json"
        },
        { Cluster.Scope, Cluster.Scope },
    };

    /// <summary>Scopes, the node each is on and its stage. A node called by
    /// a stage's word is still a node.</summary>
    public static TheoryData<string, string, string> NodesAndStages => new()
    {
        { $"{Cluster.Scope}/node/{Receiver}/receive/tcp", Receiver, "receive" },
        { $"{Cluster.Scope}/node/send/process/x", "send", "process" },
        { $"{Cluster.Scope}/round-trip/send/tcp/json", "", "send" },
        { $"{Cluster.Scope}/node", "", "" },
        { Cluster.Scope, "", "" },
        { ScopeTree.Root, "", "" },
    };

    /// <summary>
    /// The tree answers what the runtime's export answers, because it asks it:
    /// containment and a scope's parts are <c>observe::Scope</c>'s, the stage
    /// words <c>node::Stage</c>'s and the order <c>observe::Standing</c>'s,
    /// tested once where they are written, and not written here again
    /// (ADR-0052, amendment 2026-09-24). What is tested here is that the
    /// surface returns the export's answer, whatever it is.
    /// </summary>
    [Theory]
    [MemberData(nameof(Containment))]
    public void TheTreeAnswersWhatTheRuntimeExportAnswers(string candidate, string scope)
    {
        RuntimeRules rules = RuntimeLibrary.Rules;

        Assert.Equal(rules.Contains(scope, candidate), ScopeTree.Beneath(candidate, scope));
        Assert.Equal(rules.Parts(scope), ScopeTree.Parts(scope));
        Assert.Equal(rules.Parts(candidate), ScopeTree.Parts(candidate));
        Assert.Equal(rules.StageWords, ScopeTree.Stages);

        Assert.Equal(
            rules.WorstFirst(Leaves).Select(at => Leaves[at]),
            ScopeTree.WorstFirst(Leaves));
    }

    [Fact]
    public void ParentClimbsOneLevelAndStopsAtTheRoot()
    {
        Assert.Equal($"{Received}/receive", ScopeTree.Parent($"{Received}/receive/orders"));
        Assert.Equal(ScopeTree.Root, ScopeTree.Parent(Received));
        Assert.Equal(ScopeTree.Root, ScopeTree.Parent(ScopeTree.Root));
    }

    [Fact]
    public void SegmentAndNameReadTheThreeLevels()
    {
        string scope = $"{Received}/receive/orders/in";

        Assert.Equal("receive", ScopeTree.Segment(scope, 1));
        Assert.Equal("receive/orders/in", ScopeTree.Name(scope));
        Assert.Equal("receive", ScopeTree.Name($"{Received}/receive"));
    }

    /// <summary>
    /// A name is the exact scope within its cluster. Until 2026-09-26 the
    /// first two segments were dropped: the Monitor said a skipped cabinet
    /// was <c>s3/xml</c>, which test it was nowhere, and a node's leaf lost
    /// the node marker.
    /// </summary>
    [Theory]
    [MemberData(nameof(Names))]
    public void ANameIsTheScopeWithinItsCluster(string scope, string name)
    {
        Assert.Equal(name, ScopeTree.Name(scope));
    }

    /// <summary>
    /// The node a scope is on and its stage are <c>observe::Scope</c>'s, asked
    /// of the runtime: the surface returns what the export returns (open
    /// problem 25, row q). Segment 0 is the cluster and never the node.
    /// </summary>
    [Theory]
    [MemberData(nameof(NodesAndStages))]
    public void TheNodeAndStageAreWhatTheRuntimeExportAnswers(
        string scope, string node, string stage)
    {
        (string exportedNode, string exportedStage) = RuntimeLibrary.Rules.Node(scope);

        Assert.Equal(exportedNode, ScopeTree.Node(scope));
        Assert.Equal(exportedStage, ScopeTree.Stage(scope));
        Assert.Equal((node, stage), (ScopeTree.Node(scope), ScopeTree.Stage(scope)));
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
        Assert.Equal($"{Received}/receive/party", worst.Scope);
        Assert.Equal("refused", worst.Evidence);
        Assert.Equal(Now.AddSeconds(-4), worst.Observed);
    }

    [Fact]
    public void TheWorstLeafBeneathAScopeIgnoresWhatIsOutsideIt()
    {
        HealthRecord? worst = ScopeTree.WorstLeaf(Leaves, Sent);

        Assert.NotNull(worst);
        Assert.Equal(HealthState.Paused, worst.State);
        Assert.Equal("paused by ilian", worst.Evidence);
        Assert.Null(ScopeTree.WorstLeaf(Leaves, ScopeTree.Root + Processor));
    }

    [Fact]
    public void EqualsAnswerTheSameWayEveryTime()
    {
        HealthRecord[] equal =
        [
            new($"{Sent}/send/b", HealthState.Done, 50, "b", Now),
            new($"{Sent}/send/a", HealthState.Done, 50, "a", Now),
        ];

        Assert.Equal($"{Sent}/send/a", ScopeTree.Worst(equal)!.Scope);
        Assert.Equal($"{Sent}/send/a", ScopeTree.Worst(equal.Reverse())!.Scope);
    }

    [Fact]
    public void BranchesBeneathTheRootAreTheNodesRolledUp()
    {
        IReadOnlyList<Branch> branches = ScopeTree.Branches(Leaves, ScopeTree.Root);

        Assert.Equal(2, branches.Count);
        Assert.Equal(Receiver, branches[0].Label);
        Assert.Equal(HealthState.Holding, branches[0].State);
        Assert.False(branches[0].IsLeaf);
        Assert.Equal(3, branches[0].Count);
        Assert.Equal("refused", branches[0].Worst.Evidence);
        Assert.Equal(Sender, branches[1].Label);
        Assert.Equal("paused by ilian", branches[1].Worst.Evidence);
    }

    [Fact]
    public void BranchesAtTheBottomAreLeavesWithTheirOwnMood()
    {
        IReadOnlyList<Branch> branches = ScopeTree.Branches(Leaves, $"{Received}/receive");

        Assert.Equal(2, branches.Count);
        Assert.True(branches[0].IsLeaf);
        Assert.Equal(HealthState.Done, branches[0].State);
        Assert.Equal("party", branches[0].Label);
        Assert.Equal(HealthState.Fine, branches[1].State);
    }

    [Fact]
    public void TheTrailIsEveryStepBackToTheCluster()
    {
        IReadOnlyList<Crumb> trail = ScopeTree.Trail($"{Received}/receive", ScopeTree.Root);

        Assert.Equal(
            [new Crumb("cluster", ScopeTree.Root),
             new Crumb(Receiver, Received),
             new Crumb("receive", $"{Received}/receive")],
            trail);
    }

    /// <summary>
    /// A trail starts where the drill does, the cluster by its own name.
    /// Until 2026-09-26 it began at the root, so a Playground cluster read
    /// <c>cluster / &lt;cluster&gt; / …</c> — one cluster, two crumbs, and a
    /// level that held one row. A scope not beneath the top is trailed from
    /// the root.
    /// </summary>
    [Fact]
    public void TheTrailStartsAtTheClusterByItsNameAndNeverTwice()
    {
        Assert.Equal(
            [new Crumb(Cluster.Name, Cluster.Scope),
             new Crumb("node", $"{Cluster.Scope}/node"),
             new Crumb(Receiver, $"{Cluster.Scope}/node/{Receiver}")],
            ScopeTree.Trail($"{Cluster.Scope}/node/{Receiver}", Cluster.Scope));
        Assert.Equal(
            [new Crumb("cluster", ScopeTree.Root), new Crumb(Receiver, Received)],
            ScopeTree.Trail(Received, Cluster.Scope));
    }

    [Fact]
    public void EachStageCountsItsOwnThingAndNothingElseIsAStage()
    {
        // ADR-0027 clause 5: Streams at Receive, Journeys in Process, Messages
        // at Send — the three words kept apart, on every board.
        Assert.Equal(Counted.Streams, ScopeTree.CountedAt("receive"));
        Assert.Equal(Counted.Journeys, ScopeTree.CountedAt("process"));
        Assert.Equal(Counted.Messages, ScopeTree.CountedAt("send"));
        Assert.Throws<ArgumentOutOfRangeException>(() => ScopeTree.CountedAt("file"));
    }

    [Fact]
    public void ALocationPausesAndAProcessDoesNotWhereverTheStageSits()
    {
        // node::Stage::pausable and ::location, called in the runtime.
        Assert.True(ScopeTree.Pausable($"{Received}/receive/orders"));
        Assert.True(ScopeTree.Pausable($"{Cluster.Scope}/node/{Sender}/send/tcp/json"));
        Assert.False(ScopeTree.Pausable($"{Received}/process/route"));
        Assert.False(ScopeTree.Pausable($"{Received}/capability"));
        Assert.Equal("receive location", ScopeTree.Location("receive"));
        Assert.Equal("send location", ScopeTree.Location("send"));
        Assert.Null(ScopeTree.Location(string.Empty));
    }

    [Fact]
    public void TheStageIsFoundWhereverItSits()
    {
        Assert.Equal("receive", ScopeTree.Stage($"{Received}/receive/orders"));
        Assert.Equal("send", ScopeTree.Stage($"{Cluster.Scope}/round-trip/send/tcp/json"));
        Assert.Equal(string.Empty, ScopeTree.Stage($"{Cluster.Scope}/filing/file/csv"));
    }
}
