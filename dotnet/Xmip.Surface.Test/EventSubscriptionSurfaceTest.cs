using Xmip.Abi.Module;
using Xmip.Abi.Operate;

namespace Xmip.Surface.Test;

/// <summary>
/// The Event subscriptions as every surface lists and acts on them (ADR-0065,
/// amendment 2026-09-29): a snapshot lists what its publication carries and
/// leaves an act where it says its publisher takes orders, or declines where
/// it says nowhere; the one query drills, filters and sorts; every act the
/// runtime knows is a word it takes.
/// </summary>
public sealed class EventSubscriptionSurfaceTest
{
    private static string Published(string orders)
    {
        return "node = \"xmip:///CT\"\n"
            + (orders.Length > 0 ? $"orders = '{orders}'\n" : string.Empty)
            + Entry("alpha", 1, "every Event", "active", 3, "xmip:///CT/node/alpha")
            + Entry("gamma", 2, "every Event ending failure", "paused", 9, "xmip:///CT/node/gamma")
            + Entry("gamma", 4, "se.xmip.send.failure", "active", 1, "xmip:///CT");
    }

    private static string Entry(
        string node, int id, string action, string state, int queued, string scope)
    {
        return $"[[event_subscriptions]]\nnode = \"xmip:///CT/node/{node}\"\nid = {id}\n"
            + Named(id)
            + $"party = \"0199a0a0-0000-7000-8000-00000000000{id}\"\n"
            + $"action = \"{action}\"\nscope = \"{scope}\"\n"
            + $"state = \"{state}\"\nqueued = {queued}\n";
    }

    // Two declared with a name, as the Playground's are; the fourth with
    // none, as a program that subscribed by identifier alone is.
    private static string Named(int id)
    {
        return id switch
        {
            1 => "subscriber = \"operations\"\n",
            2 => "subscriber = \"on-call\"\n",
            _ => string.Empty,
        };
    }

    private static SnapshotOperator Over(string text, out string file)
    {
        file = Path.Combine(Path.GetTempPath(), $"xmip-subscriptions-{Guid.NewGuid():N}.toml");
        File.WriteAllText(file, text);
        return new SnapshotOperator(file);
    }

    [Fact]
    public void ASnapshotListsWhatItsPublicationCarriesAndLeavesAnActWhereItSays()
    {
        string orders = Path.Combine(Path.GetTempPath(), $"xmip-orders-{Guid.NewGuid():N}");
        SnapshotOperator surface = Over(Published(orders), out string file);

        try
        {
            EventSubscriptionList listed = surface.EventSubscriptions();
            Assert.Equal(orders, listed.Orders);
            Assert.Equal(3, listed.EventSubscriptions.Count);
            EventSubscriptionRecord held = listed.EventSubscriptions.Single(entry => entry.Id == 2);
            Assert.True(held.Paused);

            EventSubscriptionOperation resumed = surface.Act(held, EventSubscriptionAct.Resume, "ilian");

            Assert.True(resumed.Applied, resumed.Result);
            Assert.Contains("left for gamma", resumed.Result, StringComparison.Ordinal);
            Assert.Single(Directory.GetFiles(Path.Combine(orders, "gamma"), "*.toml"));
        }
        finally
        {
            File.Delete(file);
            Directory.Delete(orders, recursive: true);
        }
    }

    [Fact]
    public void ASnapshotWhosePublisherTakesNoOrdersDeclinesEveryAct()
    {
        SnapshotOperator surface = Over(Published(string.Empty), out string file);

        try
        {
            EventSubscriptionRecord held = surface.EventSubscriptions().EventSubscriptions[0];

            foreach (EventSubscriptionAct act in Enum.GetValues<EventSubscriptionAct>())
            {
                EventSubscriptionOperation declined = surface.Act(held, act, "ilian");
                Assert.False(declined.Applied);
                Assert.Contains("takes no orders", declined.Result, StringComparison.Ordinal);
            }
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void TheQueryDrillsFiltersAndSorts()
    {
        SnapshotOperator surface = Over(Published(string.Empty), out string file);

        try
        {
            IReadOnlyList<EventSubscriptionRecord> all = surface.EventSubscriptions().EventSubscriptions;

            // Who a subscriber is: its declared name, its identifier where
            // it was declared with none; the default order is by it.
            Assert.Equal(
                ["operations", "on-call", "0199a0a0-0000-7000-8000-000000000004"],
                all.OrderBy(s => s.Id).Select(EventSubscriptionQuery.Who));
            Assert.Equal([4ul, 2ul, 1ul], new EventSubscriptionQuery().Apply(all).Select(s => s.Id));
            Assert.Equal(
                [4ul, 2ul],
                new EventSubscriptionQuery { Location = "xmip:///CT/node/gamma" }.Apply(all)
                    .Select(s => s.Id));
            Assert.Equal(
                [4ul],
                new EventSubscriptionQuery { Location = "xmip:///CT/node/gamma", Id = 4 }.Apply(all)
                    .Select(s => s.Id));
            Assert.Equal(
                [2ul, 1ul, 4ul],
                new EventSubscriptionQuery { Sort = "queued", Order = "descending" }.Apply(all)
                    .Select(s => s.Id));
            Assert.Equal(
                [1ul],
                new EventSubscriptionQuery { Pattern = "*/alpha" }.Apply(all).Select(s => s.Id));
            Assert.Equal("CT", EventSubscriptionQuery.Cluster(all[0]));
            Assert.Equal("alpha", EventSubscriptionQuery.NodeName(all[0]));

            ArgumentException refused = Assert.Throws<ArgumentException>(
                () => new EventSubscriptionQuery { Sort = "mood" }.Apply(all));
            Assert.StartsWith("REFUSED", refused.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void EveryActIsAWordTheRuntimeTakes()
    {
        foreach (EventSubscriptionAct act in Enum.GetValues<EventSubscriptionAct>())
        {
            XmipStatus status = RuntimeLibrary.Rules.EventSubscriptions.Act(
                ulong.MaxValue, EventSubscriptionOperation.Word(act), "ilian", out string said);

            Assert.True(status == XmipStatus.NotFound, $"{act}: {status} {said}");
        }
    }
}
