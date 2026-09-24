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

    [Fact]
    public void EveryMoodIsItsOwnLowerCaseWord()
    {
        foreach (HealthState mood in Enum.GetValues<HealthState>())
        {
            Assert.Equal(mood.ToString().ToUpperInvariant(), English.Mood(mood).ToUpperInvariant());
        }
    }

    [Fact]
    public void EveryWordReadsBackAsItsMoodAndAStrangerAsNone()
    {
        foreach (HealthState mood in Enum.GetValues<HealthState>())
        {
            Assert.Equal(mood, English.MoodOf(English.Mood(mood)));
        }

        Assert.Null(English.MoodOf("green"));
        Assert.Null(English.MoodOf("Fine"));
        Assert.Null(English.MoodOf(null));
    }

    [Fact]
    public void EveryMoodHasTheColorTheRecordGivesIt()
    {
        // ADR-0041: the moods and their colors, in one sentence; here in one
        // table, so the stylesheet's classes and the prompt agree.
        Assert.Equal("green", English.Color(HealthState.Fine));
        Assert.Equal("slate", English.Color(HealthState.Paused));
        Assert.Equal("blue", English.Color(HealthState.Working));
        Assert.Equal("yellow", English.Color(HealthState.Stressed));
        Assert.Equal("burnt", English.Color(HealthState.Exhausted));
        Assert.Equal("red", English.Color(HealthState.Done));
        Assert.Equal("orange", English.Color(HealthState.Holding));
        Assert.Equal("muted", English.Color((HealthState)42));
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
    public void CountsAreFiguresAndBytesAreScaled()
    {
        Assert.Equal("–", English.Count(null));
        Assert.Equal("1,284", English.Count(new("s", Counted.Streams, 1_284, Now, Now, Now)));
        Assert.Equal("91.3 MB", English.Count(new("s", Counted.Bytes, 91_337_412, Now, Now, Now)));
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
}
