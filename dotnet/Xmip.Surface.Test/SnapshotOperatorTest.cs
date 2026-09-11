using Xmip.Abi.Operate;

namespace Xmip.Surface.Test;

/// <summary>
/// The snapshot surface over a small fixture: it reads what was published,
/// worst first, sums counts, declines to act, and says so when the file is
/// not there instead of pretending an empty estate (ADR-0052 clause 3).
/// </summary>
public sealed class SnapshotOperatorTest
{
    private static readonly string Fixture =
        Path.Combine(AppContext.BaseDirectory, "Fixture", "snapshot.toml");

    [Fact]
    public void ReadsEveryRecordWorstFirst()
    {
        SnapshotOperator surface = new(Fixture);

        IReadOnlyList<HealthRecord> all = surface.Health(ScopeTree.Root);

        Assert.Equal(5, all.Count);
        Assert.Equal("xmip:///edge-01/receive/partner", all[0].Scope);
        Assert.Equal(HealthState.Done, all[0].State);
        Assert.Equal(95, all[0].Severity);
        Assert.Equal("connection refused by partner-x (10.0.4.21:22)", all[0].Evidence);
        Assert.Equal(HealthState.Stressed, all[1].State);
        Assert.Equal(HealthState.Paused, all[2].State);
        Assert.Equal(HealthState.Fine, all[3].State);
    }

    [Fact]
    public void PausedIsAMoodNotAnEvidenceString()
    {
        SnapshotOperator surface = new(Fixture);

        HealthRecord warehouse = surface.Health("xmip:///edge-02/send/warehouse").Single();

        Assert.Equal(HealthState.Paused, warehouse.State);
    }

    [Fact]
    public void ObservedIsReadFromUnixNanos()
    {
        SnapshotOperator surface = new(Fixture);

        HealthRecord partner = surface.Health("xmip:///edge-01/receive/partner").Single();

        Assert.Equal(DateTimeOffset.UnixEpoch.AddTicks(1789111684000000000 / 100), partner.Observed);
    }

    [Fact]
    public void HealthBeneathAScopeIsOnlyWhatIsBeneathIt()
    {
        SnapshotOperator surface = new(Fixture);

        IReadOnlyList<HealthRecord> edge02 = surface.Health("xmip:///edge-02");

        Assert.Equal(2, edge02.Count);
        Assert.All(edge02, record => Assert.Equal("edge-02", ScopeTree.Node(record.Scope)));
    }

    [Fact]
    public void CountsAreSummedAtTheNodeTheSnapshotNames()
    {
        SnapshotOperator surface = new(Fixture);

        Assert.Equal(1_284UL, surface.Measure(ScopeTree.Root, Counted.Streams)!.Value);
        Assert.Equal(2_110UL, surface.Measure(ScopeTree.Root, Counted.Journeys)!.Value);
        Assert.Equal(0UL, surface.Measure(ScopeTree.Root, Counted.Messages)!.Value);
        Assert.Equal(0UL, surface.Measure("xmip:///edge-01", Counted.Streams)!.Value);
    }

    [Fact]
    public void TheSourceNamesTheFile()
    {
        SnapshotOperator surface = new(Fixture);

        Assert.True(surface.Exists);
        Assert.Equal($"SNAPSHOT — {Fixture}", surface.Source);
    }

    [Fact]
    public void AMissingFileIsSaidSoAndReportsNothing()
    {
        string missing = Path.Combine(Path.GetTempPath(), $"no-such-snapshot-{Guid.NewGuid():n}.toml");
        SnapshotOperator surface = new(missing);

        Assert.False(surface.Exists);
        Assert.Equal($"SNAPSHOT — no file at {missing}", surface.Source);
        Assert.Empty(surface.Health(ScopeTree.Root));
        Assert.Equal(0UL, surface.Measure(ScopeTree.Root, Counted.Streams)!.Value);
    }

    [Fact]
    public void AFileThatAppearsLaterIsReadOnTheNextQuery()
    {
        string later = Path.Combine(Path.GetTempPath(), $"late-snapshot-{Guid.NewGuid():n}.toml");
        SnapshotOperator surface = new(later);

        try
        {
            Assert.Empty(surface.Health(ScopeTree.Root));

            File.Copy(Fixture, later);

            Assert.Equal(5, surface.Health(ScopeTree.Root).Count);
        }
        finally
        {
            File.Delete(later);
        }
    }

    [Fact]
    public void ARecordOfWhatWasPublishedCannotBePausedOrResumed()
    {
        SnapshotOperator surface = new(Fixture);

        Assert.Contains("cannot be paused", surface.PauseScope(ScopeTree.Root, "ilian"), StringComparison.Ordinal);
        Assert.Contains("cannot be resumed", surface.ResumeScope(ScopeTree.Root), StringComparison.Ordinal);
        Assert.Equal(5, surface.Health(ScopeTree.Root).Count);
    }
}
