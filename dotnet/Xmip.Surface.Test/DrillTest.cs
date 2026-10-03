using Xmip.Abi.Operate;

namespace Xmip.Surface.Test;

/// <summary>
/// The drill every surface walks, over a Playground cluster as a roll
/// publishes one: it starts at the cluster and never above it, each stage has
/// the figures taken on it and the Locations beneath it, and every row names
/// the leaf that explains it, so the next step toward the cause is always on
/// the row (the owner, 2026-09-26: *drill-down does not work and datapoints
/// are wrong*). What the CLI, the PowerShell module and the three web views
/// show is these answers; each assertion here is a number one of them showed
/// wrong until that day.
/// </summary>
public sealed class DrillTest
{
    private static readonly string Fixture =
        Path.Combine(AppContext.BaseDirectory, "Fixture", "cluster.toml");

    private static readonly TestCluster Test = TestCluster.Read();

    // The cluster and the fixture's nodes as scopes, by what each declares.
    private static readonly string Cluster = Test.Scope;
    private static readonly string Receiving = $"{Cluster}/node/{Test.WithRole("receiving")}";
    private static readonly string Sending = $"{Cluster}/node/{Test.WithRole("sending")}";

    [Fact]
    public void TheDrillStartsAtTheClusterAndAnEmptyScopeIsIt()
    {
        IOperatorSurface surface = new SnapshotOperator(Fixture);

        Assert.Equal(Cluster, surface.Root());
        Assert.Equal(
            [Cluster],
            ScopeSelection.Of(surface, string.Empty, out _)!.Scopes);
        Assert.Equal(
            [$"{Cluster}/node"],
            surface.Children(surface.Root())
                .Select(item => item.Scope));
    }

    [Fact]
    public void AStageCountsWhatWasTakenOnItAndListsItsLocations()
    {
        IOperatorSurface surface = new SnapshotOperator(Fixture);
        ScopeIndex index = surface.Index();

        Assert.Equal([$"{Receiving}/receive"], index.StageScopes("receive"));
        Assert.Equal(
            [$"{Receiving}/receive/file", $"{Receiving}/receive/tcp"],
            surface.Locations("receive"));
        Assert.Equal(2, surface.Locations("send").Count);
        Assert.Equal(6UL, surface.Stage("receive").Streams);
        Assert.Null(surface.Stage("receive").Messages);
        Assert.Equal(6UL, surface.Stage("process").Journeys);
        Assert.Equal(5UL, surface.Stage("send").Messages);
    }

    [Fact]
    public void ALocationsOwnVerdictIsNotLostBehindWhatIsBeneathIt()
    {
        // 2026-09-26: a sending node's dns/regex Send Location was Done, its
        // identity step beneath it fine, and the drill's last step showed only
        // the fine step.
        string path = Path.Combine(Path.GetTempPath(), $"xmip-drill-{Guid.NewGuid():N}.toml");
        File.WriteAllText(path, $$"""
            node = "{{Cluster}}"
            [[records]]
            scope = "{{Sending}}/send/dns/regex"
            state = "done"
            severity = 90
            evidence = "no port was free"
            [[records]]
            scope = "{{Sending}}/send/dns/regex/identity"
            state = "fine"
            """);
        IOperatorSurface surface = new SnapshotOperator(path);
        string location = $"{Sending}/send/dns/regex";

        Assert.Equal("no port was free", surface.Index().Own(location)?.Evidence);
        Assert.Equal(["identity"], surface.Index().Branches(location).Select(b => b.Label));
        Assert.Equal(location, surface.Describe(location).Worst);
        Assert.Equal(location, surface.Describe(Cluster).Worst);
        File.Delete(path);
    }

    [Fact]
    public void ACountOffTheMessagePathIsNoFigureOfAStage()
    {
        // 2026-09-26: the Receive card said 96,968 Streams, of which a few
        // thousand were received; the rest were the daily backlog's drain.
        string path = Path.Combine(Path.GetTempPath(), $"xmip-drill-{Guid.NewGuid():N}.toml");
        File.WriteAllText(path, $$"""
            node = "{{Cluster}}"
            [[records]]
            scope = "{{Receiving}}/receive/http/json"
            state = "fine"
            [[records]]
            scope = "{{Receiving}}/daily-backlog/drain"
            state = "fine"
            [[counts]]
            counted = "streams"
            value = 3
            scope = "{{Receiving}}/receive"
            [[counts]]
            counted = "streams"
            value = 900
            scope = "{{Receiving}}/daily-backlog"
            """);
        IOperatorSurface surface = new SnapshotOperator(path);

        Assert.Equal(903UL, surface.Figures(Cluster).Streams);
        Assert.Equal(3UL, surface.Stage("receive").Streams);
        Assert.Equal(3UL, surface.MessagePath().Streams);
        Assert.Equal(3UL, surface.Figures($"{Receiving}/receive").Streams);
        File.Delete(path);
    }

    [Fact]
    public void ANodeAndItsStageHaveFiguresOfTheirOwn()
    {
        // Until 2026-09-26 a roll wrote only its sums, and every node and
        // stage showed a dash for every figure on every surface.
        IOperatorSurface surface = new SnapshotOperator(Fixture);

        Assert.Equal(6UL, surface.Figures(Receiving).Streams);

        // As old as the publication says, never "now": a stalled publisher is
        // not an idle estate (ADR-0027 clause 6).
        Assert.Equal(
            DateTimeOffset.UnixEpoch.AddTicks(1789111688000000000 / 100),
            surface.Figures(Receiving).Observed);
        Assert.Equal(5UL, surface.Figures($"{Sending}/send").Messages);
        Assert.Null(surface.Figures(Sending).Streams);
    }

    [Fact]
    public void EveryRowNamesTheLeafThatExplainsItSoTheNextStepIsOnTheRow()
    {
        IOperatorSurface surface = new SnapshotOperator(Fixture);
        string worst = $"{Sending}/send/tcp/json";

        ScopeItem cluster = surface.Describe(Cluster);
        Assert.Equal(HealthState.Holding, cluster.Health);
        Assert.Equal(worst, cluster.Worst);
        Assert.True(cluster.Troubled);

        // Step by step: each level's worst row is on the way to the leaf.
        string at = surface.Root();

        while (surface.Children(at) is { Count: > 0 } children)
        {
            ScopeItem next = children[0];
            Assert.Equal(worst, next.Worst);
            at = next.Scope;
        }

        Assert.Equal(worst, at);
        ScopeItem leaf = surface.Describe(at);
        Assert.False(leaf.IsContainer);
        Assert.Equal("2/3 rounds passed, 1 failed", leaf.Evidence);
        Assert.Equal($"node/{Test.WithRole("sending")}/send/tcp/json", leaf.Name);
    }
}
