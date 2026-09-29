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
public sealed class SubscriptionSurfaceTest
{
    private static string Published(string orders)
    {
        return "node = \"xmip:///CT\"\n"
            + (orders.Length > 0 ? $"orders = '{orders}'\n" : string.Empty)
            + Entry("R1", 1, "every Event", "active", 3, "xmip:///CT/node/R1")
            + Entry("S1", 2, "every Event ending failure", "paused", 9, "xmip:///CT/node/S1")
            + Entry("S1", 4, "se.xmip.send.failure", "active", 1, "xmip:///CT");
    }

    private static string Entry(
        string node, int id, string action, string state, int queued, string scope)
    {
        return $"[[subscriptions]]\nnode = \"xmip:///CT/node/{node}\"\nid = {id}\n"
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
            SubscriptionList listed = surface.Subscriptions();
            Assert.Equal(orders, listed.Orders);
            Assert.Equal(3, listed.Subscriptions.Count);
            SubscriptionRecord held = listed.Subscriptions.Single(entry => entry.Id == 2);
            Assert.True(held.Paused);

            SubscriptionOperation resumed = surface.Act(held, SubscriptionAct.Resume, "ilian");

            Assert.True(resumed.Applied, resumed.Result);
            Assert.Contains("left for S1", resumed.Result, StringComparison.Ordinal);
            Assert.Single(Directory.GetFiles(Path.Combine(orders, "S1"), "*.toml"));
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
            SubscriptionRecord held = surface.Subscriptions().Subscriptions[0];

            foreach (SubscriptionAct act in Enum.GetValues<SubscriptionAct>())
            {
                SubscriptionOperation declined = surface.Act(held, act, "ilian");
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
            IReadOnlyList<SubscriptionRecord> all = surface.Subscriptions().Subscriptions;

            // Who a subscriber is: its declared name, its identifier where
            // it was declared with none; the default order is by it.
            Assert.Equal(
                ["operations", "on-call", "0199a0a0-0000-7000-8000-000000000004"],
                all.OrderBy(s => s.Id).Select(SubscriptionQuery.Who));
            Assert.Equal([4ul, 2ul, 1ul], new SubscriptionQuery().Apply(all).Select(s => s.Id));
            Assert.Equal(
                [4ul, 2ul],
                new SubscriptionQuery { Location = "xmip:///CT/node/S1" }.Apply(all)
                    .Select(s => s.Id));
            Assert.Equal(
                [4ul],
                new SubscriptionQuery { Location = "xmip:///CT/node/S1", Id = 4 }.Apply(all)
                    .Select(s => s.Id));
            Assert.Equal(
                [2ul, 1ul, 4ul],
                new SubscriptionQuery { Sort = "queued", Order = "descending" }.Apply(all)
                    .Select(s => s.Id));
            Assert.Equal(
                [1ul],
                new SubscriptionQuery { Pattern = "*/R1" }.Apply(all).Select(s => s.Id));
            Assert.Equal("CT", SubscriptionQuery.Cluster(all[0]));
            Assert.Equal("R1", SubscriptionQuery.NodeName(all[0]));

            ArgumentException refused = Assert.Throws<ArgumentException>(
                () => new SubscriptionQuery { Sort = "mood" }.Apply(all));
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
        foreach (SubscriptionAct act in Enum.GetValues<SubscriptionAct>())
        {
            XmipStatus status = RuntimeLibrary.Rules.Subscriptions.Act(
                ulong.MaxValue, SubscriptionOperation.Word(act), "ilian", out string said);

            Assert.True(status == XmipStatus.NotFound, $"{act}: {status} {said}");
        }
    }
}
