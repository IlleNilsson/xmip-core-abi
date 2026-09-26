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

    [Fact]
    public void TheDrillStartsAtTheClusterAndAnEmptyScopeIsIt()
    {
        IOperatorSurface surface = new SnapshotOperator(Fixture);

        Assert.Equal("xmip:///C1", surface.Root());
        Assert.Equal(
            ["xmip:///C1"],
            ScopeSelection.Of(surface, string.Empty, out _)!.Scopes);
        Assert.Equal(
            ["xmip:///C1/node"],
            surface.Children(surface.Root())
                .Select(item => item.Scope));
    }

    [Fact]
    public void AStageCountsWhatWasTakenOnItAndListsItsLocations()
    {
        IOperatorSurface surface = new SnapshotOperator(Fixture);
        ScopeIndex index = surface.Index();

        Assert.Equal(["xmip:///C1/node/alpha/receive"], index.StageScopes("receive"));
        Assert.Equal(
            ["xmip:///C1/node/alpha/receive/file", "xmip:///C1/node/alpha/receive/tcp"],
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
        // 2026-09-26, C1: gamma's dns/regex Send Location was Done, its identity
        // step beneath it fine, and the drill's last step showed only the
        // fine step.
        string path = Path.Combine(Path.GetTempPath(), $"xmip-drill-{Guid.NewGuid():N}.toml");
        File.WriteAllText(path, """
            node = "xmip:///C1"
            [[records]]
            scope = "xmip:///C1/node/gamma/send/dns/regex"
            state = "done"
            severity = 90
            evidence = "no port was free"
            [[records]]
            scope = "xmip:///C1/node/gamma/send/dns/regex/identity"
            state = "fine"
            """);
        IOperatorSurface surface = new SnapshotOperator(path);
        const string Location = "xmip:///C1/node/gamma/send/dns/regex";

        Assert.Equal("no port was free", surface.Index().Own(Location)?.Evidence);
        Assert.Equal(["identity"], surface.Index().Branches(Location).Select(b => b.Label));
        Assert.Equal(Location, surface.Describe(Location).Worst);
        Assert.Equal(Location, surface.Describe("xmip:///C1").Worst);
        File.Delete(path);
    }

    [Fact]
    public void ACountOffTheMessagePathIsNoFigureOfAStage()
    {
        // 2026-09-26: the Receive card said 96,968 Streams, of which a few
        // thousand were received; the rest were the daily backlog's drain.
        string path = Path.Combine(Path.GetTempPath(), $"xmip-drill-{Guid.NewGuid():N}.toml");
        File.WriteAllText(path, """
            node = "xmip:///C1"
            [[records]]
            scope = "xmip:///C1/node/alpha/receive/http/json"
            state = "fine"
            [[records]]
            scope = "xmip:///C1/node/alpha/daily-backlog/drain"
            state = "fine"
            [[counts]]
            counted = "streams"
            value = 3
            scope = "xmip:///C1/node/alpha/receive"
            [[counts]]
            counted = "streams"
            value = 900
            scope = "xmip:///C1/node/alpha/daily-backlog"
            """);
        IOperatorSurface surface = new SnapshotOperator(path);

        Assert.Equal(903UL, surface.Figures("xmip:///C1").Streams);
        Assert.Equal(3UL, surface.Stage("receive").Streams);
        Assert.Equal(3UL, surface.MessagePath().Streams);
        Assert.Equal(3UL, surface.Figures("xmip:///C1/node/alpha/receive").Streams);
        File.Delete(path);
    }

    [Fact]
    public void ANodeAndItsStageHaveFiguresOfTheirOwn()
    {
        // Until 2026-09-26 a roll wrote only its sums, and every node and
        // stage showed a dash for every figure on every surface.
        IOperatorSurface surface = new SnapshotOperator(Fixture);

        Assert.Equal(6UL, surface.Figures("xmip:///C1/node/alpha").Streams);

        // As old as the publication says, never "now": a stalled publisher is
        // not an idle estate (ADR-0027 clause 6).
        Assert.Equal(
            DateTimeOffset.UnixEpoch.AddTicks(1789111688000000000 / 100),
            surface.Figures("xmip:///C1/node/alpha").Observed);
        Assert.Equal(5UL, surface.Figures("xmip:///C1/node/gamma/send").Messages);
        Assert.Null(surface.Figures("xmip:///C1/node/gamma").Streams);
    }

    [Fact]
    public void EveryRowNamesTheLeafThatExplainsItSoTheNextStepIsOnTheRow()
    {
        IOperatorSurface surface = new SnapshotOperator(Fixture);
        const string Leaf = "xmip:///C1/node/gamma/send/tcp/json";

        ScopeItem cluster = surface.Describe("xmip:///C1");
        Assert.Equal(HealthState.Holding, cluster.Health);
        Assert.Equal(Leaf, cluster.Worst);
        Assert.True(cluster.Troubled);

        // Step by step: each level's worst row is on the way to the leaf.
        string at = surface.Root();

        while (surface.Children(at) is { Count: > 0 } children)
        {
            ScopeItem next = children[0];
            Assert.Equal(Leaf, next.Worst);
            at = next.Scope;
        }

        Assert.Equal(Leaf, at);
        ScopeItem leaf = surface.Describe(at);
        Assert.False(leaf.IsContainer);
        Assert.Equal("2/3 rounds passed, 1 failed", leaf.Evidence);
        Assert.Equal("node/gamma/send/tcp/json", leaf.Name);
    }
}
