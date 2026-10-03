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

    private static readonly TestCluster Cluster = TestCluster.Read();

    // The fixture's nodes, found by what each declares in the test cluster.
    private static readonly string Receiver = Cluster.WithRole("receiving");
    private static readonly string Processor = Cluster.WithRole("processing");
    private static readonly string Sender = Cluster.WithRole("sending");

    /// <summary>The run line the cluster fixture publishes, node by node with
    /// what each was started with.</summary>
    private static readonly string Line =
        $"RoundTrip · {Cluster.Name} · nodes {Receiver}=receiving "
        + $"{Processor}=processing+sending {Sender}=sending · online {Receiver} · realistic";

    /// <summary>The fixture's three nodes in ordinal order, the order every
    /// list of them is answered in.</summary>
    private static string[] Ordered => [.. new[] { Receiver, Processor, Sender }.Order(
        StringComparer.Ordinal)];

    /// <summary>The cluster fixture is the test cluster's (ADR-0056, amendment
    /// 2026-10-03): its <c>[run]</c> names the test cluster and only nodes the
    /// test cluster configures, so a name a test takes from
    /// <see cref="TestCluster"/> is one the fixture publishes.</summary>
    [Fact]
    public void TheClusterFixtureIsTheTestClustersOwn()
    {
        RunHeader run = new SnapshotOperator(Fixture).Run();

        Assert.Equal(Cluster.Name, run.Cluster);
        Assert.NotEmpty(run.Nodes);
        Assert.All(run.Nodes, node => Assert.Contains(node, Cluster.Nodes));
    }

    [Fact]
    public void TheRunSaysWhatItWasStartedWith()
    {
        RunHeader run = new SnapshotOperator(Fixture).Run();

        Assert.True(run.Said);
        Assert.Equal(Cluster.Name, run.Cluster);
        Assert.Equal(["RoundTrip"], run.Tests);
        Assert.Equal([Receiver, Processor, Sender], run.Nodes);
        Assert.Equal(
            [$"{Receiver}=receiving", $"{Processor}=processing+sending", $"{Sender}=sending"],
            run.Roles);
        Assert.Equal([Receiver], run.Online);
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
            $"{Cluster.Name} · no nodes · calm",
            new RunHeader(Cluster.Name, [], [], [], [], "calm").Line());

        // A publisher that says no roles leaves the names bare, and the
        // line is what it always was.
        Assert.Equal(
            $"Filing · {Cluster.Name} · nodes {Receiver} · none online · harsh",
            new RunHeader(Cluster.Name, ["Filing"], [Receiver], [], [], "harsh").Line());
        Assert.Equal(NodeCapability.None, run.Capability(Receiver));
    }

    [Fact]
    public void ANodeDeclaresWhatItCanDoAndTheSurfaceAnswersForIt()
    {
        IOperatorSurface surface = new SnapshotOperator(Fixture);

        // What the node published wins, and it carries the two kinds this rig
        // does not model in the publisher's own words (ADR-0056).
        NodeCapability received = surface.Capability(Receiver);
        Assert.True(received.Said);
        Assert.True(received.Published);
        Assert.Equal(["receiving"], received.Roles);
        Assert.True(received.Online);
        Assert.Equal("receiving · online", received.Line());
        Assert.Contains(
            "authentication and runtime capability are not modelled",
            received.Evidence,
            StringComparison.Ordinal);

        // Two roles, in declaration order however the record writes them,
        // and a role is not what the node happened to serve this round.
        NodeCapability both = surface.Capability(Processor);
        Assert.Equal(["processing", "sending"], both.Roles);
        Assert.Equal(["process", "send"], both.Stages);
        Assert.Equal("processing+sending", both.Words);
        Assert.Equal("processing+sending · offline", both.Line());

        Assert.Equal(Ordered, surface.Index().Capabilities().Select(one => one.Node));
        Assert.False(surface.Capability($"{Receiver}-absent").Said);
    }

    [Fact]
    public void ANodeThatPublishedNothingFallsBackToWhatTheRunStartedItWith()
    {
        RunHeader run = new SnapshotOperator(Fixture).Run();
        NodeCapability started = run.Capability(Processor);

        Assert.False(started.Published);
        Assert.Equal("what the run started it with", started.Origin);
        Assert.Equal(["processing", "sending"], started.Roles);
        Assert.True(run.Capability(Receiver).Online);
        Assert.Equal(NodeCapability.None, run.Capability($"{Receiver}-absent"));

        // A node started with no role of its own is named alone in [run], and
        // declaring none is a choice said in words, never an absence.
        NodeCapability whole = NodeCapability.Started(Receiver);
        Assert.True(whole.Said);
        Assert.Empty(whole.Roles);
        Assert.Equal("no role · offline", whole.Line());
        Assert.Equal("no role · offline", Capability("declares no role; offline; x").Line());
        Assert.Empty(Capability("alive").Roles);

        // The three stage roles together are executing, their sum, said once.
        NodeCapability executing =
            NodeCapability.Started($"{Sender}=sending+receiving+processing");
        Assert.Equal(["executing"], executing.Roles);
        Assert.Equal(ScopeTree.Stages, executing.Stages);
        Assert.Equal(8, NodeCapability.RoleWords.Count);
        Assert.Equal("storage", NodeCapability.RoleWords[^1]);
    }

    /// <summary>
    /// The surface reads a declaration by the node crate's rule
    /// (<c>node::NodeRole::declared</c>, called in the runtime and tested
    /// there): what it takes out of a node's evidence or a <c>[run]</c> entry
    /// is handed to that rule, and a refused declaration carries the rule's
    /// own sentence, never the words that were known (open problem 25, row i).
    /// </summary>
    [Fact]
    public void ADeclarationReadsByTheNodesRuleAndARefusalIsCarriedWhole()
    {
        NodeCapability lower = Capability("declares sending,receiving; online; x");
        Assert.Equal(Receiver, lower.Node);
        Assert.Equal(["receiving", "sending"], lower.Roles);
        Assert.True(lower.Online);
        Assert.Empty(lower.Refusal);

        NodeCapability cased = Capability("declares Sending,RECEIVING; online; x");
        Assert.Empty(cased.Roles);
        Assert.Equal(Refusal("Sending,RECEIVING"), cased.Refusal);
        Assert.StartsWith("REFUSED:", cased.Refusal, StringComparison.Ordinal);
        Assert.NotEmpty(NodeCapability.Started($"{Processor}=Processing").Refusal);
        Assert.NotEmpty(NodeCapability.Started($"{Processor}=process").Refusal);

        string refused = Refusal("receiving,relay+hold");

        NodeCapability published = Capability("declares receiving,relay+hold; offline; x");
        Assert.Empty(published.Roles);
        Assert.Equal(refused, published.Refusal);
        Assert.Equal(refused, published.Line());

        NodeCapability started = NodeCapability.Started($"{Processor}=receiving+relay+hold");
        Assert.Empty(started.Roles);
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
            $"{Cluster.Scope}/node/{Receiver}/receive/tcp", "declares sending; online; x"));
        // The root's own capability scope names no node.
        Assert.Null(NodeCapability.Declared(
            $"{ScopeTree.Root}capability", "declares sending; online; x"));
        Assert.Equal(Receiver, Capability("declares sending; online; x").Node);
    }

    // The capability record the receiving node publishes, with this evidence.
    private static NodeCapability Capability(string evidence)
    {
        return NodeCapability.Declared($"{Cluster.Scope}/node/{Receiver}/capability", evidence)
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
            Ordered.Select(node => $"{Cluster.Scope}/node/{node}"),
            ((IOperatorSurface)new SnapshotOperator(Fixture)).NodeScopes());

        // A node's own publication: its nodes stand directly beneath the root.
        string plain = Path.Combine(AppContext.BaseDirectory, "Fixture", "snapshot.toml");

        Assert.Equal(
            Ordered.Select(node => ScopeTree.Root + node),
            ((IOperatorSurface)new SnapshotOperator(plain)).NodeScopes());
    }

    [Fact]
    public void TheTopologyIsClusterNodesStagesAndEndpoints()
    {
        TopologySnapshot topology = new SnapshotOperator(Fixture).Topology();

        TopologyNode cluster = Assert.Single(topology.Nodes, node => node.ParentId is null);
        Assert.Equal(TopologyNodeKind.Cluster, cluster.Kind);
        Assert.Equal(Cluster.Name, cluster.Label);
        Assert.Equal(Cluster.Scope, cluster.Scope);

        Assert.Equal(
            Ordered,
            topology.Nodes
                .Where(node => node.Kind == TopologyNodeKind.Node)
                .Select(node => node.Label)
                .Order(StringComparer.Ordinal));
        Assert.All(
            topology.Nodes.Where(node => node.Kind == TopologyNodeKind.Node),
            node => Assert.Equal("cluster", node.ParentId));
        // The processing node declared process and send, and both are drawn:
        // the send stage before it has reported on anything, as configured.
        Assert.Equal(
            new[]
            {
                $"node/{Receiver}/receive", $"node/{Processor}/process",
                $"node/{Processor}/send", $"node/{Sender}/send",
            }.Order(StringComparer.Ordinal),
            topology.Nodes
                .Where(node => node.Kind == TopologyNodeKind.Stage)
                .Select(node => node.Id)
                .Order(StringComparer.Ordinal));
        Assert.Equal(
            TopologyOrigin.Configured,
            Assert.Single(topology.Nodes, node => node.Id == $"node/{Processor}/send").Origin);

        TopologyNode endpoint = Assert.Single(
            topology.Nodes, node => node.Id == $"node/{Sender}/send/tcp");
        Assert.Equal(TopologyNodeKind.Endpoint, endpoint.Kind);
        Assert.Equal($"node/{Sender}/send", endpoint.ParentId);
        Assert.Equal(HealthState.Stressed, endpoint.State);
    }

    [Fact]
    public void TheHandoffsLinkReceiveToProcessToSend()
    {
        TopologySnapshot topology = new SnapshotOperator(Fixture).Topology();

        Assert.Equal(
            [
                ($"node/{Receiver}/receive", $"node/{Processor}/process", TopologyOrigin.Both,
                    6UL, 1.5D),
                ($"node/{Processor}/process", $"node/{Processor}/send", TopologyOrigin.Configured,
                    0UL, 0D),
                ($"node/{Processor}/process", $"node/{Sender}/send", TopologyOrigin.Both,
                    6UL, 1.5D),
            ],
            topology.Links
                .Where(link => link.Protocol == "handoff")
                .Select(link => (link.From, link.To, link.Origin, link.Volume, link.Rate)));
        Assert.All(topology.Links, link =>
            Assert.Equal(CommunicationPattern.SendReceive, link.Pattern));
    }

    [Fact]
    public void APartySendsIntoTheReceiveStagesAndTheSendStagesDeliverToOne()
    {
        // The owner, 2026-09-29: something is sending streams to a Xmip Node;
        // a Xmip Node sends streams to somethings. The kind crosses as the
        // header's value, and the side is said by the links.
        TopologySnapshot topology = new SnapshotOperator(Fixture).Topology();

        IReadOnlyList<TopologyNode> parties =
            [.. topology.Nodes.Where(node => node.Kind == TopologyNodeKind.Party)];
        Assert.Equal(
            ["party/receiving/party-x", "party/sending/party-x"],
            parties.Select(party => party.Id).Order(StringComparer.Ordinal));
        Assert.All(parties, party =>
        {
            Assert.Equal("party-x", party.Label);
            Assert.Equal("cluster", party.ParentId);
            Assert.Equal($"{Cluster.Scope}/party/party-x", party.Scope);
        });
        Assert.Equal(
            HealthState.Holding,
            parties.Single(party => party.Id == "party/receiving/party-x").State);

        Assert.Equal(
            [
                ("party/sending/party-x", $"node/{Receiver}/receive", TopologyOrigin.Both, 6UL,
                    HealthState.Fine),
                ($"node/{Processor}/send", "party/receiving/party-x", TopologyOrigin.Configured,
                    0UL, HealthState.Working),
                ($"node/{Sender}/send", "party/receiving/party-x", TopologyOrigin.Both, 6UL,
                    HealthState.Stressed),
            ],
            topology.Links
                .Where(link => link.Protocol != "handoff")
                .Select(link => (link.From, link.To, link.Origin, link.Volume, link.State)));
        Assert.Equal("party", English.Word(TopologyNodeKind.Party));
    }

    [Fact]
    public void TheNodesRollupIsTheBranchsOwnRecordAndNeverANodeBesideThem()
    {
        ScopeIndex index = new SnapshotOperator(Fixture).Index();

        Assert.Equal(
            Ordered,
            index.Branches($"{Cluster.Scope}/node")
                .Select(branch => branch.Label)
                .Order(StringComparer.Ordinal));
        Assert.Equal(["node"], index.Branches(Cluster.Scope).Select(branch => branch.Label));

        // The way to the problem ends at the leaf, not at the rollup above it.
        Assert.Equal(
            $"{Cluster.Scope}/node/{Sender}/send/tcp/json", index.Worst(Cluster.Scope)?.Scope);
        Assert.Equal(HealthState.Holding, index.Rollup($"{Cluster.Scope}/node"));
    }
}
