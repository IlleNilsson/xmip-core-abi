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
        "RoundTrip · C1 · nodes alpha=receive beta=process+send gamma=send · "
        + "online alpha · realistic";

    [Fact]
    public void TheRunSaysWhatItWasStartedWith()
    {
        RunHeader run = new SnapshotOperator(Fixture).Run();

        Assert.True(run.Said);
        Assert.Equal("C1", run.Cluster);
        Assert.Equal(["RoundTrip"], run.Tests);
        Assert.Equal(["alpha", "beta", "gamma"], run.Nodes);
        Assert.Equal(["alpha=receive", "beta=process+send", "gamma=send"], run.Capabilities);
        Assert.Equal(["alpha"], run.Online);
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
        NodeCapability received = surface.Capability("alpha");
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
        NodeCapability both = surface.Capability("beta");
        Assert.Equal(["process", "send"], both.Stages);
        Assert.Equal("process+send", both.Words);
        Assert.Equal("process+send · offline", both.Line());

        Assert.Equal(
            ["alpha", "beta", "gamma"], surface.Index().Capabilities().Select(one => one.Node));
        Assert.False(surface.Capability("omega").Said);
    }

    [Fact]
    public void ANodeThatPublishedNothingFallsBackToWhatTheRunStartedItWith()
    {
        RunHeader run = new SnapshotOperator(Fixture).Run();
        NodeCapability started = run.Capability("beta");

        Assert.False(started.Published);
        Assert.Equal("what the run started it with", started.Origin);
        Assert.Equal(["process", "send"], started.Stages);
        Assert.True(run.Capability("alpha").Online);
        Assert.Equal(NodeCapability.None, run.Capability("nobody"));

        // A node started with no stage of its own is named alone in [run], and
        // declaring none is a choice said in words, never an absence.
        NodeCapability whole = NodeCapability.Started("n1");
        Assert.True(whole.Said);
        Assert.Empty(whole.Stages);
        Assert.Equal("no stage · offline", whole.Line());
        Assert.Equal(
            "no stage · offline",
            Capability("declares no stage of the message path; offline; x").Line());
        Assert.Empty(Capability("alive").Stages);
    }

    /// <summary>
    /// The surface reads a declaration by the node crate's rule
    /// (<c>node::Stage::declared</c>, called in the runtime and tested
    /// there): what it takes out of a node's evidence or a <c>[run]</c> entry
    /// is handed to that rule, and a refused declaration carries the rule's
    /// own sentence, never the words that were known (open problem 25, row i).
    /// </summary>
    [Fact]
    public void ADeclarationReadsByTheNodesRuleAndARefusalIsCarriedWhole()
    {
        NodeCapability lower = Capability("declares send,receive; online; x");
        Assert.Equal("n1", lower.Node);
        Assert.Equal(["receive", "send"], lower.Stages);
        Assert.True(lower.Online);
        Assert.Empty(lower.Refusal);

        NodeCapability cased = Capability("declares Send,RECEIVE; online; x");
        Assert.Empty(cased.Stages);
        Assert.Equal(Refusal("Send,RECEIVE"), cased.Refusal);
        Assert.StartsWith("REFUSED:", cased.Refusal, StringComparison.Ordinal);
        Assert.NotEmpty(NodeCapability.Started("n2=Process").Refusal);

        string refused = Refusal("receive,relay+hold");

        NodeCapability published = Capability("declares receive,relay+hold; offline; x");
        Assert.Empty(published.Stages);
        Assert.Equal(refused, published.Refusal);
        Assert.Equal(refused, published.Line());

        NodeCapability started = NodeCapability.Started("n2=receive+relay+hold");
        Assert.Empty(started.Stages);
        Assert.Equal(refused, started.Refusal);
    }

    /// <summary>
    /// Only the record a node publishes at its own <c>capability</c> scope is
    /// a declaration; where that is, is <c>observe::capability</c>'s, called in
    /// the runtime, and a record anywhere else declares nothing.
    /// </summary>
    [Fact]
    public void OnlyTheRecordAtANodesCapabilityScopeIsItsDeclaration()
    {
        Assert.Null(NodeCapability.Declared(
            "xmip:///C1/node/n1/receive/tcp", "declares send; online; x"));
        Assert.Null(NodeCapability.Declared("xmip:///capability", "declares send; online; x"));
        Assert.Equal("n1", Capability("declares send; online; x").Node);
    }

    // The capability record node n1 publishes, with this evidence.
    private static NodeCapability Capability(string evidence)
    {
        return NodeCapability.Declared("xmip:///C1/node/n1/capability", evidence)
            ?? throw new InvalidOperationException("a capability record reads as one");
    }

    private static string Refusal(string words)
    {
        RuntimeLibrary.Rules.Declared(words, out string refusal);

        return refusal;
    }

    /// <summary>
    /// A cluster's nodes are what its publisher draws as nodes, three here,
    /// never the one scope beneath the root that is the cluster itself — the
    /// Monitor said "1 node(s)" over this fixture until 2026-09-25. A
    /// publication with no topology is a node's own, and its nodes are the
    /// scopes directly beneath the root.
    /// </summary>
    [Fact]
    public void TheNodesAreWhatThePublisherDrawsAsNodes()
    {
        Assert.Equal(
            ["xmip:///C1/node/alpha", "xmip:///C1/node/beta", "xmip:///C1/node/gamma"],
            ((IOperatorSurface)new SnapshotOperator(Fixture)).NodeScopes());

        string plain = Path.Combine(AppContext.BaseDirectory, "Fixture", "snapshot.toml");

        Assert.Equal(
            ["xmip:///edge-01", "xmip:///edge-02", "xmip:///lab"],
            ((IOperatorSurface)new SnapshotOperator(plain)).NodeScopes());
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
            ["alpha", "beta", "gamma"],
            topology.Nodes
                .Where(node => node.Kind == TopologyNodeKind.Node)
                .Select(node => node.Label)
                .Order(StringComparer.Ordinal));
        Assert.All(
            topology.Nodes.Where(node => node.Kind == TopologyNodeKind.Node),
            node => Assert.Equal("cluster", node.ParentId));
        // beta declared process and send, and both are drawn: the send stage
        // before it has reported on anything, as configured.
        Assert.Equal(
            ["node/alpha/receive", "node/beta/process", "node/beta/send", "node/gamma/send"],
            topology.Nodes
                .Where(node => node.Kind == TopologyNodeKind.Stage)
                .Select(node => node.Id)
                .Order(StringComparer.Ordinal));
        Assert.Equal(
            TopologyOrigin.Configured,
            Assert.Single(topology.Nodes, node => node.Id == "node/beta/send").Origin);

        TopologyNode endpoint = Assert.Single(
            topology.Nodes, node => node.Id == "node/gamma/send/tcp");
        Assert.Equal(TopologyNodeKind.Endpoint, endpoint.Kind);
        Assert.Equal("node/gamma/send", endpoint.ParentId);
        Assert.Equal(HealthState.Stressed, endpoint.State);
    }

    [Fact]
    public void TheHandoffsLinkReceiveToProcessToSend()
    {
        TopologySnapshot topology = new SnapshotOperator(Fixture).Topology();

        Assert.Equal(
            [
                ("node/alpha/receive", "node/beta/process", TopologyOrigin.Both, 6UL, 1.5D),
                ("node/beta/process", "node/beta/send", TopologyOrigin.Configured, 0UL, 0D),
                ("node/beta/process", "node/gamma/send", TopologyOrigin.Both, 6UL, 1.5D),
            ],
            topology.Links.Select(link =>
                (link.From, link.To, link.Origin, link.Volume, link.Rate)));
        Assert.All(topology.Links, link =>
        {
            Assert.Equal(CommunicationPattern.SendReceive, link.Pattern);
            Assert.Equal("handoff", link.Protocol);
        });
    }

    [Fact]
    public void TheNodesRollupIsTheBranchsOwnRecordAndNeverANodeBesideThem()
    {
        ScopeIndex index = new SnapshotOperator(Fixture).Index();

        Assert.Equal(
            ["alpha", "beta", "gamma"],
            index.Branches("xmip:///C1/node")
                .Select(branch => branch.Label)
                .Order(StringComparer.Ordinal));
        Assert.Equal(["node"], index.Branches("xmip:///C1").Select(branch => branch.Label));

        // The way to the problem ends at the leaf, not at the rollup above it.
        Assert.Equal("xmip:///C1/node/gamma/send/tcp/json", index.Worst("xmip:///C1")?.Scope);
        Assert.Equal(HealthState.Holding, index.Rollup("xmip:///C1/node"));
    }
}
