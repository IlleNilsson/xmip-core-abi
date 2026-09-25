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

    private static Published Cluster()
    {
        return new Published(
            new("xmip:///C1/node/alpha/receive/tcp", HealthState.Fine, 0, "", Now),
            new("xmip:///C1/node/alpha/receive/file", HealthState.Stressed, 55, "slow", Now),
            new("xmip:///C1/node/alpha2/receive/tcp", HealthState.Fine, 0, "", Now),
            new("xmip:///C1/node/beta/process/json", HealthState.Done, 90, "refused", Now),
            new("xmip:///C1/node/gamma/send/tcp", HealthState.Fine, 0, "", Now));
    }

    [Fact]
    public void AnArgumentWithNoWildcardIsTheScopeItself()
    {
        ScopeSelection? chosen = ScopeSelection.Of(
            Cluster(), "xmip:///C1/node/alpha", out string refusal);

        Assert.NotNull(chosen);
        Assert.False(chosen.Patterned);
        Assert.Equal(["xmip:///C1/node/alpha"], chosen.Scopes);
        Assert.Equal(string.Empty, refusal);
        Assert.Equal(ScopeSelection.Exactly("xmip:///C1/node/alpha").Scopes, chosen.Scopes);
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
        ScopeSelection? chosen = ScopeSelection.Of(Cluster(), "xmip:///C1/node/alpha*", out _);

        Assert.NotNull(chosen);
        Assert.True(chosen.Patterned);

        // alpha and alpha2 themselves, not everything beneath them as well: a command
        // reads a scope and what is under it, so a child would be said twice.
        Assert.Equal(["xmip:///C1/node/alpha", "xmip:///C1/node/alpha2"], chosen.Scopes);
    }

    [Fact]
    public void APatternThatMatchesNothingIsRefusedNamingItAndWhatThereIs()
    {
        ScopeSelection? chosen = ScopeSelection.Of(
            Cluster(), "xmip:///C1/node/Q*", out string refusal);

        Assert.Null(chosen);
        Assert.StartsWith("REFUSED", refusal, StringComparison.Ordinal);
        Assert.Contains("xmip:///C1/node/Q*", refusal, StringComparison.Ordinal);
        Assert.Contains(
            "beneath xmip:///C1/node there is: alpha, alpha2, beta, gamma",
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
