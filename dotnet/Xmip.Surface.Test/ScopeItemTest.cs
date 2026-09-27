using Xmip.Abi.Operate;

namespace Xmip.Surface.Test;

/// <summary>
/// Which rows a selection names and in what order, once for
/// <c>xmip-cli show</c> and <c>Get-XmipScope</c>: a scope exists when health
/// was recorded at or beneath it or it has a figure, and a wildcard's rows
/// come worst first. Until 2026-09-27 the cmdlet held both rules and the
/// command line neither.
/// </summary>
public sealed class ScopeItemTest
{
    private static readonly DateTimeOffset Now = new(2026, 9, 11, 12, 0, 0, TimeSpan.Zero);

    private static Published Cluster()
    {
        return new Published(
            new("xmip:///C1/node/alpha/receive/tcp", HealthState.Fine, 0, "", Now),
            new("xmip:///C1/node/beta/receive/tcp", HealthState.Fine, 0, "", Now),
            new("xmip:///C1/node/gamma/receive/file", HealthState.Stressed, 55, "slow", Now));
    }

    [Fact]
    public void AScopeWithNothingAtItDoesNotExist()
    {
        IOperatorSurface surface = Cluster();

        Assert.True(surface.Describe("xmip:///C1/node/alpha").Exists);
        Assert.False(surface.Describe("xmip:///C1/node/delta").Exists);
        Assert.Empty(ScopeItem.Selected(surface, ScopeSelection.Exactly("xmip:///C1/node/delta")));
    }

    [Fact]
    public void AWildcardsRowsComeWorstFirst()
    {
        IOperatorSurface surface = Cluster();
        ScopeSelection chosen = ScopeSelection.Of(surface, "xmip:///C1/node/*", out _)!;

        IReadOnlyList<ScopeItem> rows = ScopeItem.Selected(surface, chosen);

        Assert.Equal(
            ["xmip:///C1/node/gamma", "xmip:///C1/node/alpha", "xmip:///C1/node/beta"],
            rows.Select(row => row.Scope));
    }

    [Fact]
    public void ARowWithNoHealthSaysSoInWords()
    {
        Assert.Equal("nothing recorded", English.Mood((HealthState?)null));
        Assert.Equal(English.Mood(HealthState.Fine), English.Mood((HealthState?)HealthState.Fine));
    }

    /// <summary>A surface over records a test wrote, answering as every real
    /// surface answers: the leaves beneath a scope.</summary>
    private sealed class Published(params HealthRecord[] records) : IOperatorSurface
    {
        public string Source => "PUBLISHED — a test wrote these";

        public IReadOnlyList<HealthRecord> Health(string scope)
        {
            return ScopeTree.WorstFirst(
                records.Where(record => ScopeTree.Beneath(record.Scope, scope)));
        }

        public MeasurementRecord? Measure(string scope, Counted counted)
        {
            return null;
        }

        public string PauseScope(string scope, string who)
        {
            return "no";
        }

        public string ResumeScope(string scope)
        {
            return "no";
        }
    }
}
