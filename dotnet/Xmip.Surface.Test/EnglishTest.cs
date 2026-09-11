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
    public void TheRollupSaysNothingRecordedForNothing()
    {
        Assert.Equal("nothing recorded", English.Rollup([]));
        Assert.Equal("fine", English.Rollup([new("xmip:///n/send/a", HealthState.Fine, 0, "", Now)]));
        Assert.Equal("holding", English.Rollup([new("xmip:///n/send/a", HealthState.Done, 9, "", Now)]));
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
    public void StartingSaysWhatTheRuntimeAnswered()
    {
        Assert.Equal("started n.toml", English.Started("n.toml", XmipStatus.Ok));
        Assert.Contains(OperateAbi.StartEntrypoint, English.Started("n.toml", XmipStatus.Unsupported), StringComparison.Ordinal);
        Assert.StartsWith("n.toml refused: ", English.Started("n.toml", XmipStatus.Invalid), StringComparison.Ordinal);
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
        Assert.StartsWith("nothing to pause at xmip:///n", English.Paused("xmip:///n", XmipStatus.NotFound), StringComparison.Ordinal);
        Assert.Equal("resumed xmip:///n", English.Resumed("xmip:///n", XmipStatus.Ok));
        Assert.StartsWith("nothing to resume at xmip:///n", English.Resumed("xmip:///n", XmipStatus.NotFound), StringComparison.Ordinal);
    }
}
