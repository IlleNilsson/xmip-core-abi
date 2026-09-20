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
}
