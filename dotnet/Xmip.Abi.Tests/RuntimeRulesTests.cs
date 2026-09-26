using Xmip.Abi.Operate;

namespace Xmip.Abi.Tests;

/// <summary>
/// <see cref="RuntimeRules"/> crosses section 7 of <c>xmip_operate.h</c> to
/// the runtime this estate built — the library the binding's project copies
/// beside every test assembly — and brings back what the owning crate says.
/// The rules themselves are tested once, in Rust, where they are written
/// (<c>observe</c>'s <c>scope.rs</c> and <c>health.rs</c>, <c>node</c>'s
/// <c>stage.rs</c>); these prove the crossing: strings in and out, the fill
/// shape, a refusal carried whole and every mood.
/// </summary>
public sealed class RuntimeRulesTests
{
    private static readonly Lazy<RuntimeRules> Loaded = new(() =>
    {
        string[] beside = Directory.GetFiles(AppContext.BaseDirectory, "*xmip_core_runtime.*");

        Assert.True(
            beside.Length == 1,
            $"no runtime beside {AppContext.BaseDirectory}: cargo build in " +
            "module/platform/runtime, then build this project");

        return RuntimeRules.Load(beside[0], out string reason)
            ?? throw new InvalidOperationException(reason);
    });

    /// <summary>The runtime this estate built, loaded once for every test
    /// that crosses to it.</summary>
    internal static RuntimeRules Rules => Loaded.Value;

    [Theory]
    [InlineData("xmip:///n", "xmip:///n/receive/a", true)]
    [InlineData("xmip:///n", "xmip:///nx", false)]
    [InlineData("", "xmip:///n", true)]
    [InlineData("xmip://edge-01/n", "xmip:///n/receive", true)]
    [InlineData("xmip:///n/receive", "xmip:///n", false)]
    [InlineData("xmip:///ö/å", "xmip:///ö/å/ä", true)]
    public void ContainmentCrossesBothWays(string scope, string candidate, bool contains)
    {
        Assert.Equal(contains, Rules.Contains(scope, candidate));
    }

    [Fact]
    public void PartsComeBackAsTheSegmentsTopFirst()
    {
        Assert.Equal(
            ["edge-01", "receive", "orders"],
            Rules.Parts("xmip://lab:9000/edge-01/receive/orders/"));
        Assert.Equal(["ö", "å"], Rules.Parts("xmip:///ö/å"));
        Assert.Empty(Rules.Parts("xmip:///"));

        string deep = "xmip:///" + string.Join('/', Enumerable.Range(0, 40));

        Assert.Equal(40, Rules.Parts(deep).Length);
    }

    [Fact]
    public void TheNodeComesBackBorrowedAndTheStageStatic()
    {
        Assert.Equal(("ö", "send"), Rules.Node("xmip://lab/C1/node/ö/send/x"));
        Assert.Equal(("alpha", string.Empty), Rules.Node("xmip:///C1/node/alpha"));
        Assert.Equal((string.Empty, "receive"), Rules.Node("xmip:///C1/t/receive"));
        Assert.Equal((string.Empty, string.Empty), Rules.Node(string.Empty));
    }

    [Fact]
    public void TheStageWordsAndADeclarationCrossAndARefusalComesBackWhole()
    {
        Assert.Equal(["receive", "process", "send"], Rules.StageWords);
        Assert.Equal(["receive", "send"], Rules.Declared(" send + receive ", out string none));
        Assert.Empty(none);
        Assert.Empty(Rules.Declared(string.Empty, out _));

        Assert.Empty(Rules.Declared("Send+relay", out string refusal));
        Assert.Equal(
            "REFUSED: no capability is called Send, relay; a node declares receive, process, " +
            "send, or nothing at all.",
            refusal);

        string many = string.Join(',', Enumerable.Range(0, 100).Select(n => $"word{n}"));

        Assert.Empty(Rules.Declared(many, out string crowded));
        Assert.EndsWith("or nothing at all.", crowded, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryMoodCrossesWithAWordAColorAndBackAgain()
    {
        foreach (HealthState state in Enum.GetValues<HealthState>())
        {
            string? word = Rules.Word(state);

            Assert.NotNull(word);
            Assert.NotNull(Rules.Color(state));
            Assert.Equal(state, Rules.Named(word));
        }

        Assert.Null(Rules.Word((HealthState)42));
        Assert.Null(Rules.Color((HealthState)42));
        Assert.Null(Rules.Named("Fine"));
    }

    [Fact]
    public void TheRollupCrossesAsFineOrHolding()
    {
        foreach (HealthState state in Enum.GetValues<HealthState>())
        {
            Assert.Equal(
                state == HealthState.Fine ? HealthState.Fine : HealthState.Holding,
                Rules.Rolled(state));
        }

        Assert.Throws<InvalidOperationException>(() => Rules.Rolled((HealthState)42));
    }

    [Fact]
    public void EveryCountedKindAndEveryStageCrossWithWhatTheOwnersSay()
    {
        foreach (Counted counted in Enum.GetValues<Counted>())
        {
            Assert.Equal(counted.ToString().ToLowerInvariant(), Rules.CountedWord(counted));
        }

        Assert.Null(Rules.CountedWord((Counted)42));
        Assert.Equal(Counted.Streams, Rules.StageCounted("receive"));
        Assert.Equal(Counted.Journeys, Rules.StageCounted("process"));
        Assert.Equal(Counted.Messages, Rules.StageCounted("send"));
        Assert.Null(Rules.StageCounted("Receive"));
        Assert.Equal([true, false, true], Rules.StageWords.Select(Rules.Pausable));
        Assert.Null(Rules.Pausable("file"));
        Assert.Equal("xmip process", Rules.Location("process"));
        Assert.Null(Rules.Location("capability"));
    }

    [Fact]
    public void ACapabilityRecordAndARunEntryCrossWithTheirNameAndARefusalKeepsIt()
    {
        DeclaredCapability? said = Rules.Published(
            "xmip:///C1/node/edge-01/capability", "declares send,receive; online; x");

        Assert.NotNull(said);
        Assert.Equal(new DeclaredCapability("edge-01", said.Stages, true, string.Empty), said);
        Assert.Equal(["receive", "send"], said.Stages);
        Assert.Null(Rules.Published("xmip:///C1/node/edge-01/receive", "declares send"));

        DeclaredCapability refused = Rules.Published(
            "xmip:///C1/node/ö/capability", "declares relay; online;")!;
        Assert.Equal("ö", refused.Node);
        Assert.Empty(refused.Stages);
        Assert.StartsWith("REFUSED:", refused.Refusal, StringComparison.Ordinal);

        DeclaredCapability entry = Rules.Entry(" edge-02 =process+send");
        Assert.Equal("edge-02", entry.Node);
        Assert.Equal(["process", "send"], entry.Stages);
        Assert.Empty(Rules.Entry("edge-03").Stages);
        Assert.Equal("edge-04", Rules.Entry("edge-04=relay").Node);
        Assert.NotEmpty(Rules.Entry("edge-04=relay").Refusal);
    }

    [Fact]
    public void APublicationCrossesAsTheRuntimeReadsItAndAStrangerIsRefused()
    {
        const string Text = """
            source = "a publisher"
            node = "xmip:///C1"

            [[records]]
            scope = "xmip:///C1/node/alpha/receive/tcp"
            state = "done"
            severity = 90
            evidence = "refused"
            observed_unix_nanos = 1000

            [[records]]
            scope = "xmip:///C1/node/alpha/send/tcp"
            state = "sulking"

            [[counts]]
            counted = "journeys"
            value = 4

            [[counts]]
            counted = "throughput"
            value = 1

            [run]
            cluster = "C1"
            nodes = ["alpha", "ö"]
            capabilities = ["alpha=receive"]
            stress = "harsh"

            [topology]
            observed_unix_nanos = 5

            [[topology.nodes]]
            id = "cluster"
            kind = "virtual-machine"
            origin = "configured"
            state = "fine"

            [[topology.links]]
            id = "l"
            from = "cluster"
            to = "cluster"
            pattern = "fire-and-forget"
            attempts = 3
            """;

        Publication read = Rules.Publications.Read(Text, out string refusal)!;

        Assert.Empty(refusal);
        Assert.Equal(("a publisher", "xmip:///C1"), (read.Source, read.Node));
        Assert.Equal(
            [HealthState.Done, HealthState.Stressed],
            read.Records.Select(record => record.State));
        Assert.Equal(90, read.Records[0].Severity);
        MeasurementRecord journeys = Assert.Single(read.Counts);
        Assert.Equal((Counted.Journeys, 4UL, "xmip:///C1"),
            (journeys.Counted, journeys.Value, journeys.Scope));

        Assert.NotNull(read.Run);
        Assert.Equal(["alpha", "ö"], read.Run.Nodes);
        Assert.Empty(read.Run.Tests);
        Assert.Equal("harsh", read.Run.Stress);

        Assert.NotNull(read.Topology);
        TopologyNode cluster = Assert.Single(read.Topology.Nodes);
        Assert.Equal(TopologyNodeKind.VirtualMachine, cluster.Kind);
        Assert.Equal(("cluster", (string?)null), (cluster.Label, cluster.ParentId));
        Assert.Equal("a publisher", read.Topology.Source);
        CommunicationLink link = Assert.Single(read.Topology.Links);
        Assert.Equal(
            (CommunicationPattern.FireAndForget, TopologyOrigin.Both, HealthState.Stressed, 3U),
            (link.Pattern, link.Origin, link.State, link.Attempts));

        Assert.Null(Rules.Publications.Read("not = [toml", out string said));
        Assert.NotEmpty(said);
        Publication bare = Rules.Publications.Read(string.Empty, out _)!;
        Assert.Null(bare.Run);
        Assert.Null(bare.Topology);
    }

    /// <summary>What each topology value is called crosses whole — every
    /// value its enum defines has a word and a name — and a value the runtime
    /// does not define has neither.</summary>
    [Fact]
    public void EveryTopologyValueCrossesWithItsWordAndName()
    {
        PublicationReader reader = Rules.Publications;

        Assert.All(Enum.GetValues<TopologyNodeKind>(), kind => Assert.NotNull(reader.Words(kind)));
        Assert.All(Enum.GetValues<TopologyOrigin>(), origin => Assert.NotNull(reader.Words(origin)));
        Assert.All(
            Enum.GetValues<CommunicationPattern>(),
            pattern => Assert.NotNull(reader.Words(pattern)));

        Assert.Equal(
            new TopologyWord("virtual-machine", "virtual machine"),
            reader.Words(TopologyNodeKind.VirtualMachine));
        Assert.Equal(
            new TopologyWord("both", "configured and observed"),
            reader.Words(TopologyOrigin.Both));
        Assert.Equal(
            new TopologyWord("publish-consume", "Publish → consume"),
            reader.Words(CommunicationPattern.PublishConsume));
        Assert.Null(reader.Words((TopologyNodeKind)99));
        Assert.Null(reader.Words((TopologyOrigin)(-1)));
        Assert.Null(reader.Words((CommunicationPattern)7));
    }

    [Fact]
    public void ACurveCrossesPointByPointAndAStrangerIsRefused()
    {
        const string Text = """
            node = "xmip:///Y1"

            [[points]]
            counted = "bytes"
            observed_unix_nanos = 1000000000
            value = 1024

            [[points]]
            counted = "throughput"
            value = 1
            """;

        MeasurementRecord point = Assert.Single(Rules.Publications.Curve(Text, out string none)!);

        Assert.Empty(none);
        Assert.Equal(("xmip:///Y1", Counted.Bytes, 1024UL), (point.Scope, point.Counted, point.Value));
        Assert.Equal(DateTimeOffset.UnixEpoch.AddSeconds(1), point.Observed);
        Assert.Null(Rules.Publications.Curve("points = 3", out string said));
        Assert.NotEmpty(said);
        Assert.Empty(Rules.Publications.Curve(string.Empty, out _)!);
    }

    [Fact]
    public void ManyCrossInOneCallAndComeBackAsPositionsWorstFirst()
    {
        DateTimeOffset seen = DateTimeOffset.UnixEpoch;
        HealthRecord[] records =
        [
            new("xmip:///a", HealthState.Fine, 0, string.Empty, seen),
            new("xmip:///ö/d", HealthState.Done, 60, string.Empty, seen),
            new("xmip:///h", HealthState.Holding, 0, string.Empty, seen),
            new("xmip:///e", HealthState.Done, 90, string.Empty, seen),
        ];

        Assert.Equal([2, 3, 1, 0], Rules.WorstFirst(records));
        Assert.Empty(Rules.WorstFirst([]));
        Assert.Throws<InvalidOperationException>(() => Rules.WorstFirst(
            [new HealthRecord("xmip:///a", (HealthState)42, 0, string.Empty, seen)]));
    }

    [Fact]
    public void AMissingLibraryAnswersNullWithTheReason()
    {
        string missing = Path.Combine(Path.GetTempPath(), "no-such-xmip-runtime.dll");

        Assert.Null(RuntimeRules.Load(missing, out string reason));
        Assert.Contains(missing, reason, StringComparison.Ordinal);
    }

    [Fact]
    public void AnAuditRecordCrossesToTheCapabilityAndLandsInTheDirectoryItWasTold()
    {
        string directory = Path.Combine(
            Path.GetTempPath(), $"xmip-abi-audit-{Guid.NewGuid():n}");

        AuditOutcome outcome = Rules.Audit.Record(
            "Xmip.Abi.Tests",
            directory,
            "probe",
            AuditPhase.Begin,
            AuditSeverity.Information,
            "written by the binding's own test",
            new Dictionary<string, string> { ["url"] = "http://127.0.0.1:5087", ["ö"] = "å" });

        Assert.Equal(AuditKept.Persisted, outcome.Kept);
        Assert.Equal(string.Empty, outcome.Said);
        string text = File.ReadAllText(Path.Combine(directory, "audit.toml"));
        Assert.Contains("program = \"Xmip.Abi.Tests\"", text, StringComparison.Ordinal);
        Assert.Contains("\"url\" = \"http://127.0.0.1:5087\"", text, StringComparison.Ordinal);
        Assert.Contains("\"ö\" = \"å\"", text, StringComparison.Ordinal);
        Directory.Delete(directory, recursive: true);
    }
}
