using Xmip.Abi.Operate;

namespace Xmip.Surface.Test;

/// <summary>
/// The Subscriptions as every surface lists and acts on them (ADR-0013,
/// amendment 2026-09-30): a snapshot lists what its publication carries and
/// declines every act, which is asked at a node's door (ADR-0014, amendment
/// 2026-11-13); the one query drills, filters and sorts; every act a surface
/// offers is a word the runtime takes, and there is no remove among them.
/// </summary>
public sealed class SubscriptionSurfaceTest
{
    private static readonly TestCluster Cluster = TestCluster.Read();

    // The test cluster's processing and sending nodes, by what each declares.
    private static readonly string Processor = Cluster.WithRole("processing");
    private static readonly string Sender = Cluster.WithRole("sending");
    private static readonly string Processing = $"{Cluster.Scope}/node/{Processor}";

    private static string Published()
    {
        return $"node = \"{Cluster.Scope}\"\n"
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
        file = Path.Combine(Path.GetTempPath(), $"xmip-subscriptions-{Guid.NewGuid():N}.xmip");
        File.WriteAllText(file, text);
        return new SnapshotOperator(file);
    }

    [Fact]
    public void ASnapshotListsWhatItsPublicationCarriesAndDeclinesEveryAct()
    {
        SnapshotOperator surface = Over(Published(), out string file);

        try
        {
            SubscriptionList listed = surface.Subscriptions();
            Assert.Equal(3, listed.Subscriptions.Count);
            SubscriptionRecord held = listed.Subscriptions.Single(entry => entry.Name == "edi");
            Assert.True(held.Paused);
            Assert.Equal(9ul, held.Held);

            foreach (SubscriptionAct act in Enum.GetValues<SubscriptionAct>())
            {
                SubscriptionOperation declined = surface.Act(held, act, "ilian", caller: null);
                Assert.False(declined.Applied);
                Assert.Equal(ActOutcome.Refused, declined.Outcome);
                Assert.Equal(SnapshotOperator.NotActed, declined.Result);
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
        SnapshotOperator surface = Over(Published(), out string file);

        try
        {
            IReadOnlyList<SubscriptionRecord> all = surface.Subscriptions().Subscriptions;

            Assert.Equal(
                ["edi", "flat", "structured"],
                new SubscriptionQuery().Apply(all).Select(s => s.Name));
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
        Assert.Equal(
            ["pause", "resume"],
            Enum.GetValues<SubscriptionAct>().Select(SubscriptionOperation.Word));
    }
}
