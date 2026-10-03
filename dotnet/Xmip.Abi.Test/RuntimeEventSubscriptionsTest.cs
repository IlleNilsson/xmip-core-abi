using Xmip.Abi.Module;
using Xmip.Abi.Operate;

namespace Xmip.Abi.Test;

/// <summary>
/// <see cref="RuntimeEventSubscriptions"/> crosses section 11's operator calls to
/// the runtime this estate built and back (ADR-0065, amendment 2026-09-29):
/// an Event subscription made in this process is listed, paused, resumed
/// and removed, a second act on it is refused in words, and a publication's
/// Event subscriptions are read. The order left where a publication says is
/// one for both nouns, <see cref="RuntimeSubscriptionsTest"/>'s. What
/// pausing means for delivery is
/// <c>xmip-core-event</c>'s and tested there; these prove the crossing.
/// </summary>
public sealed class RuntimeEventSubscriptionsTest
{
    private static readonly TestCluster Cluster = TestCluster.Read();

    // The test cluster's receiving and sending nodes, by what each declares.
    private static readonly string Node = $"{Cluster.Scope}/node/{Cluster.WithRole("receiving")}";
    private static readonly string Sending = $"{Cluster.Scope}/node/{Cluster.WithRole("sending")}";

    private static RuntimeRules Rules => RuntimeRulesTest.Rules;

    [Fact]
    public void ASubscriptionIsListedPausedResumedAndRemovedAndThenRefused()
    {
        using Audited audited = new();
        using EventSubscription subscription = Rules.Events.Subscribe(
            "Xmip.Abi.Test", audited.Directory, RuntimeEventsTest.Party,
            new EventFilter { Scope = audited.Scope, Outcomes = [EventOutcome.Failure] });

        EventSubscriptionRecord mine = Mine(audited);
        Assert.Equal(Node, mine.Node);
        Assert.Equal(RuntimeEventsTest.Party, mine.Party);
        Assert.Equal(string.Empty, mine.Subscriber);
        Assert.Equal("every Event ending failure", mine.Action);
        Assert.False(mine.Paused);
        Assert.Equal("active", mine.State);

        Assert.Equal(XmipStatus.Ok, Rules.EventSubscriptions.Act(mine.Id, "pause", "ilian", out string said));
        Assert.Contains("paused by ilian", said, StringComparison.Ordinal);
        Assert.True(Mine(audited).Paused);
        Assert.Equal(1, Rules.Events.Publish(audited.Raised(EventOutcome.Failure)));
        Assert.Equal(1ul, Mine(audited).Queued);

        Assert.Equal(XmipStatus.Ok, Rules.EventSubscriptions.Act(mine.Id, "resume", "ilian", out _));
        Assert.Single(subscription.Next(TimeSpan.FromSeconds(2), 16).Events);

        Assert.Equal(XmipStatus.Invalid, Rules.EventSubscriptions.Act(mine.Id, "sulk", "ilian", out said));
        Assert.Contains("pause, resume, remove", said, StringComparison.Ordinal);
        Assert.Equal(XmipStatus.Ok, Rules.EventSubscriptions.Act(mine.Id, "remove", "ilian", out _));
        Assert.DoesNotContain(
            Rules.EventSubscriptions.Standing(Node).EventSubscriptions, entry => entry.Id == mine.Id);
        Assert.Equal(
            XmipStatus.NotFound, Rules.EventSubscriptions.Act(mine.Id, "pause", "ilian", out said));
        Assert.StartsWith("REFUSED", said, StringComparison.Ordinal);
    }

    [Fact]
    public void APublicationsEventSubscriptionsAndItsOrdersAreRead()
    {
        string text =
            $"node = \"{Cluster.Scope}\"\norders = \"shared/orders\"\n"
            + $"[[event_subscriptions]]\nnode = \"{Sending}\"\nid = 2\n"
            + "subscriber = \"operations\"\nparty = \"0199a0a0-0000-7000-8000-000000000001\"\n"
            + "action = \"every Event\"\nstate = \"paused\"\nqueued = 5\n";

        Publication read = Rules.Publications.Read(text, out _)
            ?? throw new InvalidOperationException("no publication");
        EventSubscriptionRecord held = Assert.Single(read.EventSubscriptions.EventSubscriptions);

        Assert.Equal("shared/orders", read.EventSubscriptions.Orders);
        Assert.Equal((Sending, 2ul, true, 5ul), (held.Node, held.Id, held.Paused, held.Queued));
        Assert.Equal(
            ("operations", "0199a0a0-0000-7000-8000-000000000001"), (held.Subscriber, held.Party));
    }

    private static EventSubscriptionRecord Mine(Audited audited)
    {
        return Assert.Single(
            Rules.EventSubscriptions.Standing(Node).EventSubscriptions,
            entry => entry.Scope == audited.Scope);
    }
}
