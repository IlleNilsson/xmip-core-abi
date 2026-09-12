using Xmip.Abi.Operate;

namespace Xmip.Surface.Test;

public sealed class ActivitySummaryTest
{
    [Fact]
    public void UsesXmipsFiveDomainCountsAndPreservesMissingValues()
    {
        FakeSurface surface = new(new Dictionary<Counted, ulong>
        {
            [Counted.Streams] = 12,
            [Counted.Journeys] = 10,
            [Counted.Messages] = 9,
            [Counted.Retrying] = 1,
        });

        ActivitySummary summary = surface.Activity(ScopeTree.Root);

        Assert.Equal(12UL, summary.Received);
        Assert.Equal(10UL, summary.Processed);
        Assert.Equal(9UL, summary.Sent);
        Assert.Equal(1UL, summary.Retrying);
        Assert.Null(summary.Failed);
        Assert.True(summary.HasValues);
    }

    private sealed class FakeSurface(IReadOnlyDictionary<Counted, ulong> values)
        : IOperatorSurface
    {
        private static readonly DateTimeOffset Now =
            new(2026, 9, 12, 12, 0, 0, TimeSpan.Zero);

        public string Source => "test";
        public IReadOnlyList<HealthRecord> Health(string scope) => [];
        public MeasurementRecord? Measure(string scope, Counted counted) =>
            values.TryGetValue(counted, out ulong value)
                ? new MeasurementRecord(scope, counted, value, Now, Now, Now)
                : null;
        public string PauseScope(string scope, string who) => string.Empty;
        public string ResumeScope(string scope) => string.Empty;
    }
}
