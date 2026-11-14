using Xmip.Abi.Module;
using Xmip.Abi.Operate;

namespace Xmip.Abi.Test;

/// <summary>
/// <see cref="RuntimeEventSubscriptions"/> crosses section 11's operator calls to
/// the runtime this estate built and back (ADR-0065, amendment 2026-09-29):
/// a subscription refused in this process is never listed, an act on one
/// the hub does not hold is refused in words, and a publication's Event
/// subscriptions are read. No node serves in a test's process, so no
/// subscription is made here (ADR-0019, amendment 2026-11-11); listing,
/// pausing, resuming and removing one the hub holds are proven where a node
/// serves, in the runtime's own tests.
/// </summary>
public sealed class RuntimeEventSubscriptionsTest
{
    private static readonly TestCluster Cluster = TestCluster.Read();

    // The test cluster's receiving and sending nodes, by what each declares.
    private static readonly string Node = $"{Cluster.Scope}/node/{Cluster.WithRole("receiving")}";
    private static readonly string Sending = $"{Cluster.Scope}/node/{Cluster.WithRole("sending")}";

    private static RuntimeRules Rules => RuntimeRulesTest.Rules;

    [Fact]
    public void ARefusedSubscriptionIsNeverListedAndAnActOnNoneIsRefused()
    {
        using Audited audited = new();

        Assert.Throws<UnauthorizedAccessException>(() => Rules.Events.Subscribe(
            "Xmip.Abi.Test", audited.Directory, new EventFilter { Scope = audited.Scope }));

        Assert.DoesNotContain(
            Rules.EventSubscriptions.Standing(Node).EventSubscriptions,
            entry => entry.Scope == audited.Scope);
        Assert.Equal(
            XmipStatus.NotFound,
            Rules.EventSubscriptions.Act(ulong.MaxValue, "pause", "ilian", out string said));
        Assert.StartsWith("REFUSED", said, StringComparison.Ordinal);
    }

    [Fact]
    public void APublicationsEventSubscriptionsAreRead()
    {
        string text =
            $"node = \"{Cluster.Scope}\"\n"
            + $"[[event_subscriptions]]\nnode = \"{Sending}\"\nid = 2\n"
            + "subscriber = \"operations\"\nparty = \"0199a0a0-0000-7000-8000-000000000001\"\n"
            + "action = \"every Event\"\nstate = \"paused\"\nqueued = 5\n";

        Publication read = Rules.Publications.Read(text, out _)
            ?? throw new InvalidOperationException("no publication");
        EventSubscriptionRecord held = Assert.Single(read.EventSubscriptions.EventSubscriptions);

        Assert.Equal((Sending, 2ul, true, 5ul), (held.Node, held.Id, held.Paused, held.Queued));
        Assert.Equal(
            ("operations", "0199a0a0-0000-7000-8000-000000000001"), (held.Subscriber, held.Party));
    }
}
