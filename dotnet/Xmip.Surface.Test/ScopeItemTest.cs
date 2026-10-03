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

    private static readonly TestCluster Test = TestCluster.Read();

    // Three nodes of the test cluster as scopes; the sending one is stressed.
    private static readonly string Nodes = $"{Test.Scope}/node";
    private static readonly string Receiving = $"{Nodes}/{Test.WithRole("receiving")}";
    private static readonly string Processing = $"{Nodes}/{Test.WithRole("processing")}";
    private static readonly string Sending = $"{Nodes}/{Test.WithRole("sending")}";

    private static Published Cluster()
    {
        return new Published(
            new($"{Receiving}/receive/tcp", HealthState.Fine, 0, "", Now),
            new($"{Processing}/receive/tcp", HealthState.Fine, 0, "", Now),
            new($"{Sending}/receive/file", HealthState.Stressed, 55, "slow", Now));
    }

    [Fact]
    public void AScopeWithNothingAtItDoesNotExist()
    {
        IOperatorSurface surface = Cluster();
        string absent = $"{Receiving}-absent";

        Assert.True(surface.Describe(Receiving).Exists);
        Assert.False(surface.Describe(absent).Exists);
        Assert.Empty(ScopeItem.Selected(surface, ScopeSelection.Exactly(absent)));
    }

    [Fact]
    public void AWildcardsRowsComeWorstFirst()
    {
        IOperatorSurface surface = Cluster();
        ScopeSelection chosen = ScopeSelection.Of(surface, $"{Nodes}/*", out _)!;

        IReadOnlyList<ScopeItem> rows = ScopeItem.Selected(surface, chosen);

        // The worst first, then the equals by scope.
        Assert.Equal(
            [Sending, .. new[] { Receiving, Processing }.Order(StringComparer.Ordinal)],
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
