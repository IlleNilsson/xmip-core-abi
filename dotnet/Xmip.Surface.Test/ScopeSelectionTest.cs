using Xmip.Abi.Operate;

namespace Xmip.Surface.Test;

/// <summary>
/// A scope argument that selects among scopes that exist takes a wildcard
/// (ADR-0059 clause 7, read for every surface), matched by the one
/// <see cref="ScopePattern"/>. What an argument selects is decided here once
/// for <c>xmip-cli</c> and the cmdlets alike, and a pattern that names nothing
/// is REFUSED — success with silence would read as all clear. Held in
/// <c>Xmip.Cli.Test</c> until 2026-09-24, when the rule moved here.
/// </summary>
public sealed class ScopeSelectionTest
{
    private static readonly DateTimeOffset Now = new(2026, 9, 11, 12, 0, 0, TimeSpan.Zero);

    private static readonly TestCluster Test = TestCluster.Read();

    // The test cluster's nodes by what each declares, and a sibling whose
    // name the receiving node's is a prefix of.
    private static readonly string Receiver = Test.WithRole("receiving");
    private static readonly string Longer = $"{Receiver}2";
    private static readonly string Processor = Test.WithRole("processing");
    private static readonly string Sender = Test.WithRole("sending");
    private static readonly string Nodes = $"{Test.Scope}/node";

    private static Published Cluster()
    {
        return new Published(
            new($"{Nodes}/{Receiver}/receive/tcp", HealthState.Fine, 0, "", Now),
            new($"{Nodes}/{Receiver}/receive/file", HealthState.Stressed, 55, "slow", Now),
            new($"{Nodes}/{Longer}/receive/tcp", HealthState.Fine, 0, "", Now),
            new($"{Nodes}/{Processor}/process/json", HealthState.Done, 90, "refused", Now),
            new($"{Nodes}/{Sender}/send/tcp", HealthState.Fine, 0, "", Now));
    }

    [Fact]
    public void AnArgumentWithNoWildcardIsTheScopeItself()
    {
        ScopeSelection? chosen = ScopeSelection.Of(
            Cluster(), $"{Nodes}/{Receiver}", out string refusal);

        Assert.NotNull(chosen);
        Assert.False(chosen.Patterned);
        Assert.Equal([$"{Nodes}/{Receiver}"], chosen.Scopes);
        Assert.Equal(string.Empty, refusal);
        Assert.Equal(ScopeSelection.Exactly($"{Nodes}/{Receiver}").Scopes, chosen.Scopes);
    }

    [Fact]
    public void AnOmittedArgumentIsTheCluster()
    {
        ScopeSelection? chosen = ScopeSelection.Of(Cluster(), string.Empty, out _);

        Assert.NotNull(chosen);
        Assert.Equal([ScopeTree.Root], chosen.Scopes);
    }

    [Fact]
    public void AWildcardNamesTheTopmostScopesItMatches()
    {
        ScopeSelection? chosen = ScopeSelection.Of(Cluster(), $"{Nodes}/{Receiver}*", out _);

        Assert.NotNull(chosen);
        Assert.True(chosen.Patterned);

        // The two nodes themselves, not everything beneath them as well: a command
        // reads a scope and what is under it, so a child would be said twice.
        Assert.Equal([$"{Nodes}/{Receiver}", $"{Nodes}/{Longer}"], chosen.Scopes);
    }

    [Fact]
    public void APatternThatMatchesNothingIsRefusedNamingItAndWhatThereIs()
    {
        string nothing = $"{Nodes}/{Receiver}-absent*";
        ScopeSelection? chosen = ScopeSelection.Of(Cluster(), nothing, out string refusal);
        string[] there = [.. new[] { Receiver, Longer, Processor, Sender }.Order(
            StringComparer.Ordinal)];

        Assert.Null(chosen);
        Assert.StartsWith("REFUSED", refusal, StringComparison.Ordinal);
        Assert.Contains(nothing, refusal, StringComparison.Ordinal);
        Assert.Contains(
            $"beneath {Nodes} there is: {string.Join(", ", there)}",
            refusal,
            StringComparison.Ordinal);
        Assert.Contains("source PUBLISHED", refusal, StringComparison.Ordinal);
    }

    /// <summary>A surface over records a test wrote, answering as every real
    /// surface answers: the leaves beneath a scope.</summary>
    private sealed class Published(params HealthRecord[] records) : IOperatorSurface
    {
        public string Source => "PUBLISHED — a test wrote these";

        public IReadOnlyList<HealthRecord> Health(string scope)
        {
            return [.. records.Where(record => ScopeTree.Beneath(record.Scope, scope))];
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
