using Xmip.Abi.Module;
using Xmip.Abi.Operate;

namespace Xmip.Surface.Test;

/// <summary>
/// A status becomes a sentence in one place, and a mood is the word the
/// estate uses for it. The board, the command and the cmdlet all say these.
/// </summary>
public sealed class EnglishTest
{
    private static readonly DateTimeOffset Now = new(2026, 9, 11, 12, 0, 0, TimeSpan.Zero);

    private static readonly TestCluster Cluster = TestCluster.Read();

    // A node of the test cluster as a scope beneath its cluster.
    private static readonly string Node = Cluster.NodeScope(0);

    /// <summary>A mood's word and color name are <c>observe::Health</c>'s,
    /// tested there; the surface says what the runtime's export says, and
    /// only a value the runtime does not define is its own to word.</summary>
    [Fact]
    public void EveryMoodIsSaidAndPaintedAsTheRuntimeSays()
    {
        RuntimeRules rules = RuntimeLibrary.Rules;

        foreach (HealthState mood in Enum.GetValues<HealthState>())
        {
            Assert.Equal(rules.Word(mood), English.Mood(mood));
            Assert.Equal(rules.Color(mood), English.Color(mood));
            Assert.Equal(mood, English.MoodOf(English.Mood(mood)));
        }

        Assert.Equal("unknown", English.Mood((HealthState)42));
        Assert.Equal("muted", English.Color((HealthState)42));
        Assert.Null(English.MoodOf("green"));
        Assert.Null(English.MoodOf(null));
    }

    [Fact]
    public void TheRollupSaysNothingRecordedForNothing()
    {
        HealthRecord fine = new($"{Node}/send/a", HealthState.Fine, 0, "", Now);
        HealthRecord done = new($"{Node}/send/b", HealthState.Done, 9, "", Now);

        Assert.Equal("nothing recorded", English.Rollup([]));
        Assert.Equal("fine", English.Rollup([fine]));
        Assert.Equal("holding", English.Rollup([fine, done]));
    }

    [Fact]
    public void AgeReadsInSecondsThenMinutesThenHours()
    {
        Assert.Equal("4s ago", English.Age(Now.AddSeconds(-4), Now));
        Assert.Equal("3m ago", English.Age(Now.AddMinutes(-3), Now));
        Assert.Equal("2h ago", English.Age(Now.AddHours(-2.5), Now));
        Assert.Equal("0s ago", English.Age(Now.AddSeconds(5), Now));
    }

    [Fact]
    public void FiguresAreSeparatedAndBytesAreScaled()
    {
        Assert.Equal("–", English.Figure(null));
        Assert.Equal("1,284", English.Figure(1_284));
        Assert.Equal("512 B", English.Bytes(512));
        Assert.Equal("1.5 kB", English.Bytes(1_500));
        Assert.Equal("2.0 GB", English.Bytes(2_000_000_000));
    }

    [Fact]
    public void AFigureIsGroupedAndAnUnpublishedOneIsADashNeverAZero()
    {
        // The one spelling the cli, the board and the topology inspector give
        // a count; until 2026-09-24 each wrote its own.
        Assert.Equal("–", English.Figure(null));
        Assert.Equal("0", English.Figure(0));
        Assert.Equal("1,234,567", English.Figure(1_234_567));
    }

    [Fact]
    public void NothingAtAScopeNamesWhereItLooked()
    {
        Assert.Equal(
            $"Nothing at {Cluster.Scope} (SNAPSHOT — a file).",
            English.NothingAt(Cluster.Scope, "SNAPSHOT — a file"));
    }

    [Fact]
    public void PlanningSaysWhatTheRuntimeAnsweredAndThatNothingRuns()
    {
        Assert.Equal(
            "planned cluster.xmip: validated and published, not running",
            English.Planned("cluster.xmip", XmipStatus.Ok));
        Assert.Contains(
            OperateAbi.StartEntrypoint,
            English.Planned("cluster.xmip", XmipStatus.Unsupported),
            StringComparison.Ordinal);
        Assert.StartsWith(
            "cluster.xmip refused: ",
            English.Planned("cluster.xmip", XmipStatus.Invalid),
            StringComparison.Ordinal);
    }

    [Fact]
    public void ValidatingListsTheProblems()
    {
        Assert.Equal(
            "cluster.xmip is valid",
            English.Validated("cluster.xmip", new(XmipStatus.Ok, [])));
        Assert.Equal(
            "cluster.xmip is invalid: no name; no cluster",
            English.Validated("cluster.xmip", new(XmipStatus.Invalid, ["no name", "no cluster"])));
        Assert.Equal(
            $"cluster.xmip is invalid: {XmipStatus.Io.Explain()}",
            English.Validated("cluster.xmip", new(XmipStatus.Io, [])));
    }

    [Fact]
    public void PausingAndResumingSayTheScope()
    {
        Assert.Equal($"paused {Node}", English.Paused(Node, XmipStatus.Ok));
        Assert.StartsWith(
            $"nothing to pause at {Node}",
            English.Paused(Node, XmipStatus.NotFound),
            StringComparison.Ordinal);
        Assert.Equal($"resumed {Node}", English.Resumed(Node, XmipStatus.Ok));
        Assert.StartsWith(
            $"nothing to resume at {Node}",
            English.Resumed(Node, XmipStatus.NotFound),
            StringComparison.Ordinal);
    }

    [Fact]
    public void FlowSaysTheRateOrThatThereIsNoIntervalYet()
    {
        Assert.Equal("38/s", English.Flow(38.4));
        Assert.Equal("0.5/s", English.Flow(0.5));
        Assert.Equal("0/s", English.Flow(0));
        Assert.Equal("waiting for the next round", English.Flow(null));
    }

    [Fact]
    public void ARateIsOnOneLadderAndATrickleIsNeverZero()
    {
        // One ladder for every surface: the prompt writes it bare, the rest
        // with its unit. Until 2026-09-27 the prompt kept a second formatter.
        Assert.Equal("0", English.Moving(0));
        Assert.Equal("0.3", English.Moving(0.3));
        Assert.Equal("9.9", English.Moving(9.94));
        Assert.Equal("240", English.Moving(240));
        Assert.Equal("1.2K", English.Moving(1234));
        Assert.Equal("1.2M", English.Moving(1_234_567));
        Assert.Equal("1.2K/s", English.Rate(1234));

        // One Message in a publisher's half-minute window (ADR-0052,
        // amendment 2026-11-10): a trickle, never stalled.
        Assert.NotEqual("0/s", English.Rate(0.033));
        Assert.Equal("0.03/s", English.Rate(0.033));
        Assert.Equal("0.03", English.Moving(1 / 30D));
        Assert.Equal("0.05", English.Moving(0.049));
        Assert.Equal("0.1", English.Moving(0.05));
        Assert.Equal("<0.01", English.Moving(0.001));
    }

    [Fact]
    public void AStageIsNamedFromItsWordAndTheProcessStageIsTheWorkProcess()
    {
        Assert.Equal(
            ["Receive", "Work Process", "Send"], ScopeTree.Stages.Select(English.Stage));
    }

    [Fact]
    public void ALinkSaysItsVolumeAndRateOrThatNothingHasPassed()
    {
        // The owner, 2026-09-25: the topology did not show configured traffic
        // or its usage. A link says both on its line.
        Assert.Equal("1,877 · 3.1/s", English.Traffic(Link(TopologyOrigin.Both, 1_877, 3.14)));
        Assert.Equal("40 · 12/s", English.Traffic(Link(TopologyOrigin.Observed, 40, 12.4)));
        Assert.Equal(
            "configured · no traffic observed",
            English.Traffic(Link(TopologyOrigin.Configured, 0, 0)));
        Assert.Equal("0/s", English.Rate(0));
    }

    /// <summary>A link a relation explains says the relation before its
    /// figure, in the runtime's name for it (the owner, 2026-11-12: <i>Related
    /// by its role in the cluster, not by name</i>); traffic no relation
    /// explains says the figure alone.</summary>
    [Fact]
    public void ALinkSaysHowItsEndsAreRelatedBeforeItsFigure()
    {
        Assert.Equal(
            "copies to · 48,213 · 310/s",
            English.Traffic(Link(TopologyOrigin.Both, 48_213, 310) with
            {
                Relation = TopologyRelation.CopiesTo,
            }));
        Assert.Equal(
            "writes into · configured · no traffic observed",
            English.Traffic(Link(TopologyOrigin.Configured, 0, 0) with
            {
                Relation = TopologyRelation.WritesInto,
            }));
        Assert.Equal(
            "40 · 12/s",
            English.Traffic(Link(TopologyOrigin.Observed, 40, 12.4) with
            {
                Relation = TopologyRelation.Traffic,
            }));
        Assert.Equal(
            "48,213 · 310/s",
            English.Passed(Link(TopologyOrigin.Both, 48_213, 310) with
            {
                Relation = TopologyRelation.CopiesTo,
            }));
    }

    /// <summary>What a node is for is <c>node::NodeRole::purpose</c>'s, tested
    /// there; the surface says what the runtime's export says, a list of roles
    /// as their declaration, and a word that is no role its refusal.</summary>
    [Fact]
    public void ANodesPurposeIsWhatTheRuntimeSaysItsRolesAreFor()
    {
        string both = English.Purpose("operational,monitoring");

        Assert.Equal(RuntimeLibrary.Rules.Purpose("operational,monitoring"), both);
        Assert.Equal(both, English.Purpose(["operational", "monitoring"]));
        Assert.Equal(2, both.Split('\n').Length);
        Assert.Equal("declares no role", English.Purpose(string.Empty));
        Assert.Equal("declares no role", English.Purpose([]));
        Assert.StartsWith("REFUSED", English.Purpose("relay"), StringComparison.Ordinal);
    }

    /// <summary>What a topology value is called is <c>observe::topology</c>'s,
    /// tested there; the surface says what the runtime's export says. Until
    /// 2026-09-25 the web GUI kept its own list of both.</summary>
    [Fact]
    public void EveryTopologyValueIsCalledWhatTheRuntimeCallsIt()
    {
        PublicationReader reader = RuntimeLibrary.Rules.Publications;

        foreach (TopologyNodeKind kind in Enum.GetValues<TopologyNodeKind>())
        {
            Assert.Equal(
                reader.Words(kind), new TopologyWord(English.Word(kind), English.Name(kind)));
        }

        foreach (TopologyOrigin origin in Enum.GetValues<TopologyOrigin>())
        {
            Assert.Equal(
                reader.Words(origin), new TopologyWord(English.Word(origin), English.Name(origin)));
        }

        foreach (CommunicationPattern pattern in Enum.GetValues<CommunicationPattern>())
        {
            Assert.Equal(
                reader.Words(pattern),
                new TopologyWord(English.Word(pattern), English.Name(pattern)));
        }

        foreach (TopologyRelation relation in Enum.GetValues<TopologyRelation>())
        {
            Assert.Equal(
                reader.Words(relation),
                new TopologyWord(English.Word(relation), English.Name(relation)));
        }

        Assert.Equal("unknown", English.Name((TopologyRelation)99));
        Assert.Equal("virtual machine", English.Name(TopologyNodeKind.VirtualMachine));
        Assert.Equal("unknown", English.Name((TopologyNodeKind)99));
        Assert.Equal("unknown", English.Word((CommunicationPattern)99));
    }

    [Fact]
    public void AValueOrAPercentageIsSaidAsAPersonReadsIt()
    {
        Assert.Equal("—", English.Value(null));
        Assert.Equal("—", English.Value(" "));
        Assert.Equal("handoff", English.Value("handoff"));
        Assert.Equal(English.Percent(1), English.Percent(3));
        Assert.Equal(English.Percent(0), English.Percent(-1));
        Assert.StartsWith("50", English.Percent(0.5), StringComparison.Ordinal);
    }

    [Fact]
    public void TheAuditIsSaidInTheCapabilitysWordsToTheSecond()
    {
        Assert.Equal(
            "2026-09-29T14:00:00Z", English.ToTheSecond("2026-09-29T14:00:00.123456789Z"));
        Assert.Equal(
            "2026-09-29T14:00:00+02:00", English.ToTheSecond("2026-09-29T14:00:00.5+02:00"));
        Assert.Equal("2026-09-29T14:00:00Z", English.ToTheSecond("2026-09-29T14:00:00Z"));
        Assert.Equal(
            "REFUSED: severity \"loud\" is none of these.",
            English.Refusal(new ArgumentException(
                "REFUSED: severity \"loud\" is none of these.", "query")));
        Assert.Contains("operating system's log", English.NoAuditDirectory(),
            StringComparison.Ordinal);
    }

    private static CommunicationLink Link(TopologyOrigin origin, ulong volume, double rate)
    {
        return new CommunicationLink(
            "l", "a", "b", CommunicationPattern.SendReceive, origin, "handoff",
            HealthState.Fine, volume, rate, 0, 0, 0, string.Empty);
    }
}
