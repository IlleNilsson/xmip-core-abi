using Xmip.Abi.Operate;

namespace Xmip.Surface.Test;

/// <summary>
/// The snapshot surface over a Playground cluster as a roll publishes one
/// since 2026-09-19: the run says what it was started with, the topology is
/// cluster, nodes, stages and endpoints with the handoffs between the nodes,
/// and the nodes' rollup is the record of the branch they hang under.
/// </summary>
public sealed class ClusterSnapshotTest
{
    private static readonly string Fixture =
        Path.Combine(AppContext.BaseDirectory, "Fixture", "cluster.toml");

    [Fact]
    public void TheRunSaysWhatItWasStartedWith()
    {
        RunHeader run = new SnapshotOperator(Fixture).Run();

        Assert.True(run.Said);
        Assert.Equal("C1", run.Cluster);
        Assert.Equal(["RoundTrip"], run.Tests);
        Assert.Equal(["R1", "P1", "S1"], run.Nodes);
        Assert.Equal(["R1"], run.Online);
        Assert.Equal("RoundTrip · C1 · nodes R1 P1 S1 · online R1 · realistic", run.Line());
    }

    [Fact]
    public void ASnapshotWithNoRunTableSaysNothingOfItsRun()
    {
        string plain = Path.Combine(AppContext.BaseDirectory, "Fixture", "snapshot.toml");
        RunHeader run = new SnapshotOperator(plain).Run();

        Assert.False(run.Said);
        Assert.Equal(string.Empty, run.Line());
        Assert.Equal(
            "C2 · no nodes · calm",
            new RunHeader("C2", [], [], [], "calm").Line());
        Assert.Equal(
            "Filing · C2 · nodes node-01 · none online · harsh",
            new RunHeader("C2", ["Filing"], ["node-01"], [], "harsh").Line());
    }

    [Fact]
    public void TheTopologyIsClusterNodesStagesAndEndpoints()
    {
        TopologySnapshot topology = new SnapshotOperator(Fixture).Topology();

        TopologyNode cluster = Assert.Single(topology.Nodes, node => node.ParentId is null);
        Assert.Equal(TopologyNodeKind.Cluster, cluster.Kind);
        Assert.Equal("C1", cluster.Label);
        Assert.Equal("xmip:///C1", cluster.Scope);

        Assert.Equal(
            ["P1", "R1", "S1"],
            topology.Nodes
                .Where(node => node.Kind == TopologyNodeKind.Node)
                .Select(node => node.Label)
                .Order(StringComparer.Ordinal));
        Assert.All(
            topology.Nodes.Where(node => node.Kind == TopologyNodeKind.Node),
            node => Assert.Equal("cluster", node.ParentId));
        Assert.Equal(
            ["process", "receive", "send"],
            topology.Nodes
                .Where(node => node.Kind == TopologyNodeKind.Stage)
                .Select(node => node.Label)
                .Order(StringComparer.Ordinal));

        TopologyNode endpoint = Assert.Single(
            topology.Nodes, node => node.Id == "node/S1/send/tcp");
        Assert.Equal(TopologyNodeKind.Endpoint, endpoint.Kind);
        Assert.Equal("node/S1/send", endpoint.ParentId);
        Assert.Equal(HealthState.Stressed, endpoint.State);
    }

    [Fact]
    public void TheHandoffsLinkReceiveToProcessToSend()
    {
        TopologySnapshot topology = new SnapshotOperator(Fixture).Topology();

        Assert.Equal(
            [("node/R1/receive", "node/P1/process"), ("node/P1/process", "node/S1/send")],
            topology.Links.Select(link => (link.From, link.To)));
        Assert.All(topology.Links, link =>
        {
            Assert.Equal(CommunicationPattern.SendReceive, link.Pattern);
            Assert.Equal("handoff", link.Protocol);
            Assert.Equal(6UL, link.Volume);
        });
    }

    [Fact]
    public void TheNodesRollupIsTheBranchsOwnRecordAndNeverANodeBesideThem()
    {
        ScopeIndex index = new SnapshotOperator(Fixture).Index();

        Assert.Equal(
            ["P1", "R1", "S1"],
            index.Branches("xmip:///C1/node")
                .Select(branch => branch.Label)
                .Order(StringComparer.Ordinal));
        Assert.Equal(["node"], index.Branches("xmip:///C1").Select(branch => branch.Label));

        // The way to the problem ends at the leaf, not at the rollup above it.
        Assert.Equal("xmip:///C1/node/S1/send/tcp/json", index.Worst("xmip:///C1")?.Scope);
        Assert.Equal(HealthState.Holding, index.Rollup("xmip:///C1/node"));
    }
}
