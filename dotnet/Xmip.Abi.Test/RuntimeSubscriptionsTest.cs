using Xmip.Abi.Module;
using Xmip.Abi.Operate;

namespace Xmip.Abi.Test;

/// <summary>
/// <see cref="RuntimeSubscriptions"/> crosses section 14 to the runtime this
/// estate built and back (ADR-0013, amendment 2026-09-30): no node runs in a
/// test's process, so the list is empty and an act on a node is refused in
/// words; remove is no act on a Subscription and is refused as one; an order
/// on either noun is left where a publication says and nowhere else; and a
/// publication's Subscriptions are read. What pausing holds is the runtime's
/// and tested there; these prove the crossing.
/// </summary>
public sealed class RuntimeSubscriptionsTest
{
    private static readonly TestCluster Cluster = TestCluster.Read();

    // The test cluster's processing node, by what it declares.
    private static readonly string Processor = Cluster.WithRole("processing");
    private static readonly string Node = $"{Cluster.Scope}/node/{Processor}";

    private static RuntimeRules Rules => RuntimeRulesTest.Rules;

    [Fact]
    public void NoNodeRunsHereSoNothingIsListedAndAnActIsRefusedAndRemoveIsNoAct()
    {
        Assert.Empty(Rules.Subscriptions.Standing(Node).Subscriptions);

        Assert.Equal(
            XmipStatus.NotFound,
            Rules.Subscriptions.Act(Node, "structured", "pause", "ilian", out string said));
        Assert.StartsWith("REFUSED", said, StringComparison.Ordinal);

        Assert.Equal(
            XmipStatus.Invalid,
            Rules.Subscriptions.Act(Node, "structured", "remove", "ilian", out said));
        Assert.Contains("TOML configuration", said, StringComparison.Ordinal);
    }

    [Fact]
    public void AnOrderOnEitherNounIsLeftWhereThePublicationSaysAndNowhereElse()
    {
        string orders = Path.Combine(Path.GetTempPath(), $"xmip-abi-orders-{Guid.NewGuid():n}");

        try
        {
            Assert.Equal(
                XmipStatus.Ok,
                Rules.Subscriptions.Order(
                    orders, Node, "subscription", "structured", "pause", "ilian", out string file));
            Assert.True(File.Exists(file), file);
            Assert.StartsWith(Path.Combine(orders, Processor), file, StringComparison.Ordinal);
            Assert.Equal(
                XmipStatus.Ok,
                Rules.Subscriptions.Order(
                    orders, Node, "event-subscription", "3", "remove", "ilian", out _));

            Assert.Equal(
                XmipStatus.Invalid,
                Rules.Subscriptions.Order(
                    orders,
                    Node,
                    "subscription",
                    "structured",
                    "remove",
                    "ilian",
                    out string said));
            Assert.Contains("TOML configuration", said, StringComparison.Ordinal);
            Assert.Equal(
                XmipStatus.Invalid,
                Rules.Subscriptions.Order(
                    string.Empty, Node, "subscription", "structured", "pause", "ilian", out _));
            Assert.Equal(
                XmipStatus.Invalid,
                Rules.Subscriptions.Order(
                    orders, Cluster.Scope, "subscription", "structured", "pause", "ilian", out _));
        }
        finally
        {
            Directory.Delete(orders, recursive: true);
        }
    }

    [Fact]
    public void APublicationsSubscriptionsAndItsOrdersAreRead()
    {
        string text =
            $"node = \"{Cluster.Scope}\"\norders = \"shared/orders\"\n"
            + $"[[subscriptions]]\nnode = \"{Node}\"\nname = \"structured\"\n"
            + "application = \"RoundTrip\"\nfilter = \"MessageType = 'json'\"\n"
            + "destination = \"the Send Port 'RoundTripOut'\"\nstate = \"paused\"\n"
            + "by = \"ilian\"\npicked_up = 7\nheld = 5\n";

        Publication read = Rules.Publications.Read(text, out _)
            ?? throw new InvalidOperationException("no publication");
        SubscriptionRecord held = Assert.Single(read.Subscriptions.Subscriptions);

        Assert.Equal("shared/orders", read.Subscriptions.Orders);
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
