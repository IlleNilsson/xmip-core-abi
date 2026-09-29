using Xmip.Abi.Module;
using Xmip.Abi.Operate;

namespace Xmip.Abi.Test;

/// <summary>
/// <see cref="RuntimeSubscriptions"/> crosses section 11's operator calls to
/// the runtime this estate built and back (ADR-0065, amendment 2026-09-29):
/// a subscription made in this process is listed, paused, resumed and
/// removed, a second act on it is refused in words, and an order left where
/// a publication says is written. What pausing means for delivery is
/// <c>xmip-core-event</c>'s and tested there; these prove the crossing.
/// </summary>
public sealed class RuntimeSubscriptionsTest
{
    private const string Node = "xmip:///CT/node/R1";

    private static RuntimeRules Rules => RuntimeRulesTest.Rules;

    [Fact]
    public void ASubscriptionIsListedPausedResumedAndRemovedAndThenRefused()
    {
        using Audited audited = new();
        using EventSubscription subscription = Rules.Events.Subscribe(
            "Xmip.Abi.Test", audited.Directory, RuntimeEventsTest.Party,
            new EventFilter { Scope = audited.Scope, Outcomes = [EventOutcome.Failure] });

        SubscriptionRecord mine = Mine(audited);
        Assert.Equal(Node, mine.Node);
        Assert.Equal(RuntimeEventsTest.Party, mine.Party);
        Assert.Equal(string.Empty, mine.Subscriber);
        Assert.Equal("every Event ending failure", mine.Action);
        Assert.False(mine.Paused);
        Assert.Equal("active", mine.State);

        Assert.Equal(XmipStatus.Ok, Rules.Subscriptions.Act(mine.Id, "pause", "ilian", out string said));
        Assert.Contains("paused by ilian", said, StringComparison.Ordinal);
        Assert.True(Mine(audited).Paused);
        Assert.Equal(1, Rules.Events.Publish(audited.Raised(EventOutcome.Failure)));
        Assert.Equal(1ul, Mine(audited).Queued);

        Assert.Equal(XmipStatus.Ok, Rules.Subscriptions.Act(mine.Id, "resume", "ilian", out _));
        Assert.Single(subscription.Next(TimeSpan.FromSeconds(2), 16).Events);

        Assert.Equal(XmipStatus.Invalid, Rules.Subscriptions.Act(mine.Id, "sulk", "ilian", out said));
        Assert.Contains("pause, resume, remove", said, StringComparison.Ordinal);
        Assert.Equal(XmipStatus.Ok, Rules.Subscriptions.Act(mine.Id, "remove", "ilian", out _));
        Assert.DoesNotContain(
            Rules.Subscriptions.Standing(Node).Subscriptions, entry => entry.Id == mine.Id);
        Assert.Equal(
            XmipStatus.NotFound, Rules.Subscriptions.Act(mine.Id, "pause", "ilian", out said));
        Assert.StartsWith("REFUSED", said, StringComparison.Ordinal);
    }

    [Fact]
    public void AnOrderIsLeftWhereThePublicationSaysAndNowhereElse()
    {
        string orders = Path.Combine(Path.GetTempPath(), $"xmip-abi-orders-{Guid.NewGuid():n}");

        try
        {
            Assert.Equal(
                XmipStatus.Ok,
                Rules.Subscriptions.Order(orders, Node, 3, "pause", "ilian", out string file));
            Assert.True(File.Exists(file), file);
            Assert.StartsWith(Path.Combine(orders, "R1"), file, StringComparison.Ordinal);

            Assert.Equal(
                XmipStatus.Invalid,
                Rules.Subscriptions.Order(string.Empty, Node, 3, "pause", "ilian", out _));
            Assert.Equal(
                XmipStatus.Invalid,
                Rules.Subscriptions.Order(orders, "xmip:///CT", 3, "pause", "ilian", out _));
        }
        finally
        {
            Directory.Delete(orders, recursive: true);
        }
    }

    [Fact]
    public void APublicationsSubscriptionsAndItsOrdersAreRead()
    {
        const string Text =
            "node = \"xmip:///CT\"\norders = \"shared/orders\"\n"
            + "[[subscriptions]]\nnode = \"xmip:///CT/node/S1\"\nid = 2\n"
            + "subscriber = \"operations\"\nparty = \"0199a0a0-0000-7000-8000-000000000001\"\n"
            + "action = \"every Event\"\nstate = \"paused\"\nqueued = 5\n";

        Publication read = Rules.Publications.Read(Text, out _)
            ?? throw new InvalidOperationException("no publication");
        SubscriptionRecord held = Assert.Single(read.Subscriptions.Subscriptions);

        Assert.Equal("shared/orders", read.Subscriptions.Orders);
        Assert.Equal(("xmip:///CT/node/S1", 2ul, true, 5ul), (held.Node, held.Id, held.Paused, held.Queued));
        Assert.Equal(
            ("operations", "0199a0a0-0000-7000-8000-000000000001"), (held.Subscriber, held.Party));
    }

    private static SubscriptionRecord Mine(Audited audited)
    {
        return Assert.Single(
            Rules.Subscriptions.Standing(Node).Subscriptions,
            entry => entry.Scope == audited.Scope);
    }
}
