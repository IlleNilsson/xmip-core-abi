namespace Xmip.Surface.Test;

/// <summary>
/// What a scope is moving now, per second, between two publications (ADR-0052,
/// amendment 2026-09-20). One calculation, so the prompt and any other face
/// read one rate rather than each computing its own.
/// </summary>
public sealed class FigureFlowTest
{
    private static Figures At(ulong streams, ulong messages, ulong journeys)
    {
        return new Figures("xmip:///", streams, messages, journeys, null, null, null, null);
    }

    [Fact]
    public void TwoPublicationsAreARatePerSecondOverTheTimeBetweenThem()
    {
        FigureFlow flow = FigureFlow.Between(
            At(5_600, 700, 720), At(5_000, 640, 660), TimeSpan.FromSeconds(5));

        Assert.True(flow.Known);
        Assert.Equal(120, flow.Streams);
        Assert.Equal(12, flow.Messages);
        Assert.Equal(12, flow.Journeys);
    }

    [Fact]
    public void OnePublicationIsNoIntervalAndSoNoRate()
    {
        // Not known yet is not stalled, so it is null — which every surface
        // already shows as absent — and never zero.
        FigureFlow first = FigureFlow.Between(At(5_000, 640, 660), null, TimeSpan.Zero);

        Assert.False(first.Known);
        Assert.Null(first.Streams);
        Assert.Null(first.Messages);
        Assert.Null(first.Journeys);
        Assert.Equal(FigureFlow.Unknown, first);
    }

    [Fact]
    public void ZeroIsStalledAndIsSaidAsZero()
    {
        FigureFlow stalled = FigureFlow.Between(
            At(5_000, 640, 660), At(5_000, 640, 660), TimeSpan.FromSeconds(2));

        Assert.True(stalled.Known);
        Assert.Equal(0, stalled.Streams);
        Assert.Equal(0, stalled.Messages);
        Assert.Equal(0, stalled.Journeys);
    }

    [Fact]
    public void AFigureNeitherPublicationCarriesHasNoRate()
    {
        Figures now = new("xmip:///", 5_600, null, 720, null, null, null, null);
        Figures before = new("xmip:///", 5_000, null, 660, null, null, null, null);
        FigureFlow flow = FigureFlow.Between(now, before, TimeSpan.FromSeconds(10));

        Assert.Equal(60, flow.Streams);
        Assert.Null(flow.Messages);
        Assert.Equal(6, flow.Journeys);
    }

    [Fact]
    public void ACountThatFellIsAPublisherThatStartedOverAndNotAnInterval()
    {
        // A roll ends and another begins on the same path: the two figures
        // belong to two runs, and the difference between them is not work.
        FigureFlow flow = FigureFlow.Between(
            At(12, 3, 4), At(5_000, 640, 660), TimeSpan.FromSeconds(2));

        Assert.Null(flow.Streams);
        Assert.Null(flow.Messages);
        Assert.Null(flow.Journeys);
    }

    [Fact]
    public void ANegativeOrMissingIntervalIsUnknown()
    {
        Assert.Equal(
            FigureFlow.Unknown,
            FigureFlow.Between(At(1, 1, 1), At(0, 0, 0), TimeSpan.Zero));
        Assert.Equal(
            FigureFlow.Unknown,
            FigureFlow.Between(At(1, 1, 1), At(0, 0, 0), TimeSpan.FromSeconds(-3)));
    }

    [Fact]
    public void RetryingAndFailedAreRatesOnTheSameRuleAsTheStages()
    {
        // The owner named all five letters when he capped the numbers, and T
        // and F were left as totals on the assistant's judgement rather than
        // his (2026-09-20). A retry total says a run has had trouble; a retry
        // rate says it is having trouble now.
        FigureFlow flow = FigureFlow.Between(
            Troubled(9, 3), Troubled(1, 1), TimeSpan.FromSeconds(4));

        Assert.Equal(2, flow.Retrying);
        Assert.Equal(0.5, flow.Failed);
        Assert.True(flow.Known);
    }

    [Fact]
    public void ATroubleFigureNobodyPublishedHasNoRateEither()
    {
        FigureFlow flow = FigureFlow.Between(
            At(5_000, 640, 660), At(12, 3, 4), TimeSpan.FromSeconds(2));

        Assert.Null(flow.Retrying);
        Assert.Null(flow.Failed);
    }

    [Fact]
    public void AFigureAndItsRateAreAnsweredByTheKindCounted()
    {
        Figures now = Troubled(9, 3);
        FigureFlow flow = FigureFlow.Between(now, Troubled(1, 1), TimeSpan.FromSeconds(4));

        Assert.Equal(9UL, now.Of(Xmip.Abi.Operate.Counted.Retrying));
        Assert.Equal(1UL, now.Of(Xmip.Abi.Operate.Counted.Journeys));
        Assert.Null(now.Of(Xmip.Abi.Operate.Counted.Bytes));
        Assert.Equal(2, flow.Of(Xmip.Abi.Operate.Counted.Retrying));
        Assert.Null(flow.Of(Xmip.Abi.Operate.Counted.Bytes));
    }

    [Fact]
    public void AWatchSaysNothingUntilItHasAnIntervalAndKeepsTheRateBefore()
    {
        FigureWatch watch = new();
        DateTimeOffset start = DateTimeOffset.UnixEpoch;

        Assert.Equal(FigureFlow.Unknown, watch.See(At(5_000, 640, 660), start, 1));

        FigureFlow first = watch.See(At(5_600, 700, 720), start.AddSeconds(5), 2);
        Assert.Equal(120, first.Streams);
        Assert.Null(watch.Before);

        // The same publication read again — a redraw — is no interval.
        Assert.Same(first, watch.See(At(5_600, 700, 720), start.AddSeconds(9), 2));

        FigureFlow stalled = watch.See(At(5_600, 700, 720), start.AddSeconds(7), 3);
        Assert.Equal(0, stalled.Streams);
        Assert.Same(first, watch.Before);
    }

    [Fact]
    public void AWatchWithNoFeedTakesEveryReadAsAPublication()
    {
        FigureWatch watch = new();
        DateTimeOffset start = DateTimeOffset.UnixEpoch;

        watch.See(At(10, 0, 0), start);

        Assert.Equal(5, watch.See(At(20, 0, 0), start.AddSeconds(2)).Streams);
    }

    // A publication that carries a retry total and a failure total as well as
    // the three stages.
    private static Figures Troubled(ulong retrying, ulong failed)
    {
        return new Figures("xmip:///", 1, 1, 1, null, retrying, failed, null);
    }
}
