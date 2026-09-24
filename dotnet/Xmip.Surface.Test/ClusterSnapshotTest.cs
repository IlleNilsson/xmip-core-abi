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

    /// <summary>The run line the cluster fixture publishes, node by node with
    /// what each was started with.</summary>
    private const string Line =
        "RoundTrip · C1 · nodes R1=receive P1=process+send S1=send · online R1 · realistic";

    [Fact]
    public void TheRunSaysWhatItWasStartedWith()
    {
        RunHeader run = new SnapshotOperator(Fixture).Run();

        Assert.True(run.Said);
        Assert.Equal("C1", run.Cluster);
        Assert.Equal(["RoundTrip"], run.Tests);
        Assert.Equal(["R1", "P1", "S1"], run.Nodes);
        Assert.Equal(["R1=receive", "P1=process+send", "S1=send"], run.Capabilities);
        Assert.Equal(["R1"], run.Online);
        Assert.Equal(Line, run.Line());
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
            new RunHeader("C2", [], [], [], [], "calm").Line());

        // A publisher older than capabilities leaves the names bare, and the
        // line is what it always was.
        Assert.Equal(
            "Filing · C2 · nodes node-01 · none online · harsh",
            new RunHeader("C2", ["Filing"], ["node-01"], [], [], "harsh").Line());
        Assert.Equal(NodeCapability.None, run.Capability("node-01"));
    }

    [Fact]
    public void ANodeDeclaresWhatItCanDoAndTheSurfaceAnswersForIt()
    {
        IOperatorSurface surface = new SnapshotOperator(Fixture);

        // What the node published wins, and it carries the two kinds this rig
        // does not model in the publisher's own words (ADR-0056).
        NodeCapability received = surface.Capability("R1");
        Assert.True(received.Said);
        Assert.True(received.Published);
        Assert.Equal(["receive"], received.Stages);
        Assert.True(received.Online);
        Assert.Equal("receive · online", received.Line());
        Assert.Contains(
            "authentication and runtime capability are not modelled",
            received.Evidence,
            StringComparison.Ordinal);

        // Two stages, in message-path order however the record writes them,
        // and capability is not what the node happened to serve this round.
        NodeCapability both = surface.Capability("P1");
        Assert.Equal(["process", "send"], both.Stages);
        Assert.Equal("process+send", both.Words);
        Assert.Equal("process+send · offline", both.Line());

        Assert.Equal(["P1", "R1", "S1"], surface.Index().Capabilities().Select(one => one.Node));
        Assert.False(surface.Capability("S9").Said);
    }

    [Fact]
    public void ANodeThatPublishedNothingFallsBackToWhatTheRunStartedItWith()
    {
        RunHeader run = new SnapshotOperator(Fixture).Run();
        NodeCapability started = run.Capability("P1");

        Assert.False(started.Published);
        Assert.Equal("what the run started it with", started.Origin);
        Assert.Equal(["process", "send"], started.Stages);
        Assert.True(run.Capability("R1").Online);
        Assert.Equal(NodeCapability.None, run.Capability("nobody"));

        // A node started with no stage of its own is named alone in [run], and
        // declaring none is a choice said in words, never an absence.
        NodeCapability whole = NodeCapability.Started("n1");
        Assert.True(whole.Said);
        Assert.Empty(whole.Stages);
        Assert.Equal("no stage · offline", whole.Line());
        Assert.Equal(
            "no stage · offline",
            NodeCapability.Declared("n1", "declares no stage of the message path; offline; x")
                .Line());
        Assert.Empty(NodeCapability.Declared("n1", "alive").Stages);
    }

    /// <summary>
    /// The surface reads a declaration by the rule the node crate holds
    /// (<c>node::Stage::declared</c>): each word exact lowercase only (the
    /// owner, 2026-09-24), and any other word refuses the whole declaration
    /// in the same words, never dropped (open problem 25, row i).
    /// </summary>
    [Fact]
    public void ADeclarationReadsByTheNodesRuleLowercaseOnlyAndAnUnknownWordRefused()
    {
        NodeCapability lower = NodeCapability.Declared("n1", "declares send,receive; online; x");
        Assert.Equal(["receive", "send"], lower.Stages);
        Assert.Empty(lower.Refusal);

        NodeCapability cased = NodeCapability.Declared("n1", "declares Send,RECEIVE; online; x");
        Assert.Empty(cased.Stages);
        Assert.Equal(
            "REFUSED: no capability is called Send, RECEIVE; a node declares "
                + "receive, process, send, or nothing at all.",
            cased.Refusal);
        Assert.NotEmpty(NodeCapability.Started("n2=Process").Refusal);

        const string Refused = "REFUSED: no capability is called relay, hold; a node declares "
            + "receive, process, send, or nothing at all.";

        NodeCapability published = NodeCapability.Declared(
            "n1", "declares receive,relay+hold; offline; x");
        Assert.Empty(published.Stages);
        Assert.Equal(Refused, published.Refusal);
        Assert.Equal(Refused, published.Line());

        NodeCapability started = NodeCapability.Started("n2=receive+relay+hold");
        Assert.Empty(started.Stages);
        Assert.Equal(Refused, started.Refusal);
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
