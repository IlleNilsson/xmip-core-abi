using Xmip.Abi.Operate;

namespace Xmip.Abi.Test;

/// <summary>
/// <see cref="RuntimeSubscriptions"/> crosses section 14 to the runtime this
/// estate built and back (ADR-0013, amendment 2026-09-30): no node runs in a
/// test's process, so the list is empty; and a publication's Subscriptions
/// are read. What pausing holds is the runtime's and tested there, and a
/// pause or resume is asked at a node's door (ADR-0014, amendment
/// 2026-11-13); these prove the crossing.
/// </summary>
public sealed class RuntimeSubscriptionsTest
{
    private static readonly TestCluster Cluster = TestCluster.Read();

    // The test cluster's processing node, by what it declares.
    private static readonly string Processor = Cluster.WithRole("processing");
    private static readonly string Node = $"{Cluster.Scope}/node/{Processor}";

    private static RuntimeRules Rules => RuntimeRulesTest.Rules;

    [Fact]
    public void NoNodeRunsHereSoNothingIsListed()
    {
        Assert.Empty(Rules.Subscriptions.Standing(Node).Subscriptions);
    }

    [Fact]
    public void APublicationsSubscriptionsAreRead()
    {
        string text =
            $"node = \"{Cluster.Scope}\"\n"
            + $"[[subscriptions]]\nnode = \"{Node}\"\nname = \"structured\"\n"
            + "application = \"RoundTrip\"\nfilter = \"MessageType = 'json'\"\n"
            + "destination = \"the Send Port 'RoundTripOut'\"\nstate = \"paused\"\n"
            + "by = \"ilian\"\npicked_up = 7\nheld = 5\n";

        Publication read = Rules.Publications.Read(text, out _)
            ?? throw new InvalidOperationException("no publication");
        SubscriptionRecord held = Assert.Single(read.Subscriptions.Subscriptions);

        Assert.Equal(
            (Node, "structured", true, 5ul, 7ul),
            (held.Node, held.Name, held.Paused, held.Held, held.PickedUp));
        Assert.Equal(("RoundTrip", "ilian"), (held.Application, held.By));
        Assert.Empty(read.EventSubscriptions.EventSubscriptions);
    }

    /// <summary>
    /// ADR-0013 and ADR-0027, amendments 2026-11-11: a Subscription crosses
    /// with the Send Ports its destination reaches, and what the publication
    /// draws crosses with where its work came from and went, every word the
    /// runtime's (<c>xmip_publication_from_to_v1</c>).
    /// </summary>
    [Fact]
    public void ASubscriptionsSendPortsAndADrawnThingsFromAndToCross()
    {
        string location = $"{Node}/receive/In";
        string text =
            $"node = \"{Cluster.Scope}\"\n"
            + $"[[records]]\nscope = \"{location}\"\nstate = \"fine\"\n"
            + $"[[subscriptions]]\nnode = \"{Node}\"\nname = \"onward\"\n"
            + "send_ports = [\"Out\"]\n"
            + "[topology]\nobserved_unix_nanos = 5\n"
            + $"[[topology.nodes]]\nid = \"node/{Processor}/receive/In\"\nkind = \"endpoint\"\n"
            + $"scope = \"{location}\"\n"
            + $"[[topology.links]]\nid = \"in\"\nfrom = \"node/{Processor}/receive/In\"\n"
            + $"to = \"node/{Processor}/process/onward\"\n";

        Publication read = Rules.Publications.Read(text, out string refusal)
            ?? throw new InvalidOperationException(refusal);

        Assert.Equal(["Out"], Assert.Single(read.Subscriptions.Subscriptions).Reaches);
        FromTo path = Assert.Contains(location, read.Topology!.Paths!);
        Assert.Equal($"In [{Processor}]", path.From);
        Assert.Equal($"Out", path.To);
        Assert.Equal($"from In [{Processor}] → to Out", path.Line);
        Assert.Equal(
            ["Receive Location", "Subscription", "Send Port"],
            path.Hops.Select(hop => hop.Kind));
        Assert.Equal("no Subscription", path.Unrouted);
    }
}
