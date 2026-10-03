using Xmip.Abi.Module;
using Xmip.Abi.Operate;

namespace Xmip.Surface.Test;

/// <summary>
/// The Subscriptions as every surface lists and acts on them (ADR-0013,
/// amendment 2026-09-30): a snapshot lists what its publication carries and
/// leaves a pause or a resume where it says its publisher takes orders, or
/// declines where it says nowhere; the one query drills, filters and sorts;
/// every act a surface offers is a word the runtime takes, and there is no
/// remove among them.
/// </summary>
public sealed class SubscriptionSurfaceTest
{
    private static readonly TestCluster Cluster = TestCluster.Read();

    // The test cluster's processing and sending nodes, by what each declares.
    private static readonly string Processor = Cluster.WithRole("processing");
    private static readonly string Sender = Cluster.WithRole("sending");
    private static readonly string Processing = $"{Cluster.Scope}/node/{Processor}";

    private static string Published(string orders)
    {
        return $"node = \"{Cluster.Scope}\"\n"
            + (orders.Length > 0 ? $"orders = '{orders}'\n" : string.Empty)
            + Entry(Processor, "structured", "active", 12, 0)
            + Entry(Processor, "edi", "paused", 3, 9)
            + Entry(Sender, "flat", "active", 40, 0);
    }

    private static string Entry(string node, string name, string state, int picked, int held)
    {
        return $"[[subscriptions]]\nnode = \"{Cluster.Scope}/node/{node}\"\nname = \"{name}\"\n"
            + "application = \"RoundTrip\"\n"
            + $"filter = \"MessageType = '{name}'\"\n"
            + "destination = \"the Send Port 'RoundTripOut'\"\n"
            + $"state = \"{state}\"\npicked_up = {picked}\nheld = {held}\n";
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
            SubscriptionRecord held = listed.Subscriptions.Single(entry => entry.Name == "edi");
            Assert.True(held.Paused);
            Assert.Equal(9ul, held.Held);

            SubscriptionOperation resumed = surface.Act(held, SubscriptionAct.Resume, "ilian");

            Assert.True(resumed.Applied, resumed.Result);
            Assert.Contains($"left for {Processor}", resumed.Result, StringComparison.Ordinal);
            Assert.Single(Directory.GetFiles(Path.Combine(orders, Processor), "*.toml"));
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

            Assert.Equal(
                ["edi", "flat", "structured"], new SubscriptionQuery().Apply(all).Select(s => s.Name));
            Assert.Equal(
                ["edi", "structured"],
                new SubscriptionQuery { Location = Processing }.Apply(all)
                    .Select(s => s.Name));
            Assert.Equal(
                ["structured"],
                new SubscriptionQuery { Location = Processing, Name = "structured" }
                    .Apply(all).Select(s => s.Name));
            Assert.Equal(
                ["edi", "structured", "flat"],
                new SubscriptionQuery { Sort = "held", Order = "descending" }.Apply(all)
                    .Select(s => s.Name));
            Assert.Equal(
                ["flat", "structured", "edi"],
                new SubscriptionQuery { Sort = "picked-up", Order = "descending" }.Apply(all)
                    .Select(s => s.Name));
            Assert.Equal(
                ["flat"],
                new SubscriptionQuery { Pattern = $"*/{Sender}" }.Apply(all).Select(s => s.Name));
            Assert.Equal(
                ["edi"],
                new SubscriptionQuery { Pattern = "*/subscription/ed?" }.Apply(all)
                    .Select(s => s.Name));
            Assert.Equal(Cluster.Name, SubscriptionQuery.Cluster(all[0]));
            Assert.Equal(Processor, SubscriptionQuery.NodeName(all[0]));

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
    public void EveryActIsAWordTheRuntimeTakesAndNoneRemoves()
    {
        Assert.Equal(["Pause", "Resume"], Enum.GetNames<SubscriptionAct>());

        foreach (SubscriptionAct act in Enum.GetValues<SubscriptionAct>())
        {
            XmipStatus status = RuntimeLibrary.Rules.Subscriptions.Act(
                Processing, "structured", SubscriptionOperation.Word(act), "ilian",
                out string said);

            Assert.True(status == XmipStatus.NotFound, $"{act}: {status} {said}");
        }

        XmipStatus removed = RuntimeLibrary.Rules.Subscriptions.Act(
            Processing, "structured", "remove", "ilian", out string refusal);
        Assert.Equal(XmipStatus.Invalid, removed);
        Assert.Contains("TOML configuration", refusal, StringComparison.Ordinal);
    }
}
