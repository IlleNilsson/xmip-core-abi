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
        HealthRecord fine = new("xmip:///n/send/a", HealthState.Fine, 0, "", Now);
        HealthRecord done = new("xmip:///n/send/b", HealthState.Done, 9, "", Now);

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
            "Nothing at xmip:///C1 (SNAPSHOT — a file).",
            English.NothingAt("xmip:///C1", "SNAPSHOT — a file"));
    }

    [Fact]
    public void StartingSaysWhatTheRuntimeAnswered()
    {
        Assert.Equal("started n.toml", English.Started("n.toml", XmipStatus.Ok));
        Assert.Contains(
            OperateAbi.StartEntrypoint,
            English.Started("n.toml", XmipStatus.Unsupported),
            StringComparison.Ordinal);
        Assert.StartsWith(
            "n.toml refused: ",
            English.Started("n.toml", XmipStatus.Invalid),
            StringComparison.Ordinal);
    }

    [Fact]
    public void ValidatingListsTheProblems()
    {
        Assert.Equal("n.toml is valid", English.Validated("n.toml", new(XmipStatus.Ok, [])));
        Assert.Equal(
            "n.toml is invalid: no name; no cluster",
            English.Validated("n.toml", new(XmipStatus.Invalid, ["no name", "no cluster"])));
        Assert.Equal(
            $"n.toml is invalid: {XmipStatus.Io.Explain()}",
            English.Validated("n.toml", new(XmipStatus.Io, [])));
    }

    [Fact]
    public void PausingAndResumingSayTheScope()
    {
        Assert.Equal("paused xmip:///n", English.Paused("xmip:///n", XmipStatus.Ok));
        Assert.StartsWith(
            "nothing to pause at xmip:///n",
            English.Paused("xmip:///n", XmipStatus.NotFound),
            StringComparison.Ordinal);
        Assert.Equal("resumed xmip:///n", English.Resumed("xmip:///n", XmipStatus.Ok));
        Assert.StartsWith(
            "nothing to resume at xmip:///n",
            English.Resumed("xmip:///n", XmipStatus.NotFound),
            StringComparison.Ordinal);
    }

    [Fact]
    public void FlowSaysTheIncreaseAndTheRate()
    {
        Assert.Equal("+2,310 last round · 38/s", English.Flow(2_310, TimeSpan.FromSeconds(60)));
        Assert.Equal("+3 last round · 0.5/s", English.Flow(3, TimeSpan.FromSeconds(6)));
        Assert.Equal("waiting for the next round", English.Flow(0, TimeSpan.Zero));
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
        Assert.Equal("0.0/s", English.Rate(0));
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
            Assert.Equal(reader.Words(kind), new TopologyWord(English.Word(kind), English.Name(kind)));
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

    private static CommunicationLink Link(TopologyOrigin origin, ulong volume, double rate)
    {
        return new CommunicationLink(
            "l", "a", "b", CommunicationPattern.SendReceive, origin, "handoff",
            HealthState.Fine, volume, rate, 0, 0, 0, string.Empty);
    }
}
