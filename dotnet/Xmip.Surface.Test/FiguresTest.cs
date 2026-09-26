using Xmip.Abi.Operate;

namespace Xmip.Surface.Test;

/// <summary>
/// The six figures keep the record's words and its silence: a figure the
/// publisher never published is absent, not zero (ADR-0027 clause 5 and its
/// amendment of 2026-09-14). Pause and resume go through the one
/// <see cref="ScopeOperation"/> shape.
/// </summary>
public sealed class FiguresTest
{
    [Fact]
    public void ASumAddsWhatWasPublishedAndKeepsWhatNobodyPublishedAbsent()
    {
        Figures sum = Figures.Sum(
            "receive",
            [
                new Figures("a", 3, null, null, null, null, 0, null),
                new Figures("b", 4, null, null, null, null, null, null),
            ]);

        Assert.Equal(("receive", 7UL, 0UL), (sum.Scope, sum.Streams!.Value, sum.Failed!.Value));
        Assert.Null(sum.Messages);
        Assert.False(Figures.Sum("send", []).HasValues);
    }

    [Fact]
    public void ReadsTheSixCountedThingsAndKeepsUnpublishedOnesAbsent()
    {
        IOperatorSurface surface = new FakeSurface(new Dictionary<Counted, ulong>
        {
            [Counted.Streams] = 12,
            [Counted.Messages] = 9,
            [Counted.Journeys] = 10,
            [Counted.Retrying] = 1,
        });

        Figures figures = surface.Figures(ScopeTree.Root);

        Assert.Equal(12UL, figures.Streams);
        Assert.Equal(9UL, figures.Messages);
        Assert.Equal(10UL, figures.Journeys);
        Assert.Null(figures.Bytes);
        Assert.Equal(1UL, figures.Retrying);
        Assert.Null(figures.Failed);
        Assert.True(figures.HasValues);
    }

    [Fact]
    public void NothingPublishedIsNoFigures()
    {
        IOperatorSurface surface = new FakeSurface(new Dictionary<Counted, ulong>());

        Figures figures = surface.Figures("xmip:///edge-01");

        Assert.False(figures.HasValues);
        Assert.Null(figures.Observed);
    }

    [Fact]
    public void ARowCarriesTheFiguresAndTheName()
    {
        IOperatorSurface surface = new FakeSurface(
            new Dictionary<Counted, ulong> { [Counted.Streams] = 3 });

        ScopeItem row = surface.Describe("xmip:///edge-01/receive/orders");

        Assert.Equal("receive/orders", row.Name);
        Assert.False(row.IsContainer);
        Assert.Null(row.Health);
        Assert.Equal(3UL, row.Figures.Streams);
    }

    [Fact]
    public void ControlSaysWhatTheSurfaceSaid()
    {
        IOperatorSurface surface = new FakeSurface(new Dictionary<Counted, ulong>());

        ScopeOperation paused = surface.Control("xmip:///edge-01", ScopeAction.Pause, "test");
        ScopeOperation resumed = surface.Control("xmip:///edge-01", ScopeAction.Resume, "test");

        Assert.True(paused.Applied);
        Assert.Equal("paused xmip:///edge-01 by test", paused.Result);
        Assert.Equal(ScopeAction.Resume, resumed.Action);
        Assert.Equal("resumed xmip:///edge-01", resumed.Result);
    }

    private sealed class FakeSurface(IReadOnlyDictionary<Counted, ulong> values)
        : IOperatorSurface
    {
        private static readonly DateTimeOffset Now =
            new(2026, 9, 12, 12, 0, 0, TimeSpan.Zero);

        public string Source => "test";

        public IReadOnlyList<HealthRecord> Health(string scope)
        {
            return [];
        }

        public MeasurementRecord? Measure(string scope, Counted counted)
        {
            return values.TryGetValue(counted, out ulong value)
                ? new MeasurementRecord(scope, counted, value, Now, Now, Now)
                : null;
        }

        public string PauseScope(string scope, string who)
        {
            return $"paused {scope} by {who}";
        }

        public string ResumeScope(string scope)
        {
            return $"resumed {scope}";
        }
    }
}
