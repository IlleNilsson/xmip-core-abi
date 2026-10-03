using Microsoft.AspNetCore.Builder;
using Xmip.Abi.Module;
using Xmip.Abi.Operate;

namespace Xmip.Surface.Test;

/// <summary>
/// The Dead Message Queues as every surface lists them and replays from them
/// (ADR-0052, amendment 2026-10-01): a snapshot lists what its publication
/// carries — each entry's gate verdicts, promoted properties and declines in
/// the order written — and leaves a Replay where it says its publisher takes
/// orders, or declines where it says nowhere; the one query drills, filters
/// and sorts; Replay is the one act and a word the runtime takes; and a
/// Replay from elsewhere passes the one role check, taken as the proven
/// caller and never the name given (ADR-0009, amendment 2026-10-03).
/// </summary>
public sealed class DeadMessageSurfaceTest : IDisposable
{
    // The name a caller claims, which no act may carry.
    private const string Claimed = "claimed-by-the-caller";

    private static readonly TestCluster Cluster = TestCluster.Read();

    // The test cluster's receiving and processing nodes, by what each declares.
    private static readonly string Receiver = Cluster.WithRole("receiving");
    private static readonly string Processor = Cluster.WithRole("processing");
    private static readonly string Receiving = $"{Cluster.Scope}/node/{Receiver}";

    private readonly string scratch =
        Path.Combine(Path.GetTempPath(), $"xmip-dead-{Guid.NewGuid():N}");

    public DeadMessageSurfaceTest()
    {
        Directory.CreateDirectory(scratch);
    }

    private string Orders => Path.Combine(scratch, "orders");

    private string Audit => Path.Combine(scratch, "audit");

    [Fact]
    public void ASnapshotListsWhatItsPublicationCarriesAndLeavesAReplayWhereItSays()
    {
        SnapshotOperator surface = Over(Orders);

        DeadMessageList listed = surface.DeadMessages();
        Assert.Equal(Orders, listed.Orders);
        Assert.Equal(3, listed.DeadMessages.Count);
        DeadMessageRecord invoice = listed.DeadMessages.Single(entry => entry.Message == "m-2");
        Assert.Equal(
            (Receiving, 2ul, "orders"), (invoice.Node, invoice.Sequence, invoice.ReceiveLocation));
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(2_000), invoice.Received);
        Assert.Equal(
            [new("schema", "valid"), new("signature", "valid")], invoice.Validation);
        Assert.Equal([new("MessageType", "Invoice")], invoice.Promoted);
        Assert.Equal(
            [new("structured", "MessageType is Invoice, not json"), new("edi", "paused")],
            invoice.Declines);

        DeadMessageOperation replayed = surface.Act(invoice, DeadMessageAct.Replay, "ilian");

        Assert.True(replayed.Applied, replayed.Result);
        Assert.Equal(
            $"replay of Message m-2 left for {Receiver} to take at its next look",
            replayed.Result);
        string order = Assert.Single(Directory.GetFiles(Path.Combine(Orders, Receiver), "*.toml"));
        Assert.Contains(
            DeadMessageOperation.Noun, File.ReadAllText(order), StringComparison.Ordinal);
    }

    [Fact]
    public void ASnapshotWhosePublisherTakesNoOrdersDeclinesTheReplay()
    {
        SnapshotOperator surface = Over(string.Empty);
        DeadMessageRecord kept = surface.DeadMessages().DeadMessages[0];

        DeadMessageOperation declined = surface.Act(kept, DeadMessageAct.Replay, "ilian");

        Assert.False(declined.Applied);
        Assert.Contains("takes no orders", declined.Result, StringComparison.Ordinal);
    }

    [Fact]
    public void TheQueryDrillsFiltersAndSorts()
    {
        IReadOnlyList<DeadMessageRecord> all = Over(string.Empty).DeadMessages().DeadMessages;

        Assert.Equal(["m-1", "m-2", "m-3"], new DeadMessageQuery().Apply(all).Select(Id));
        Assert.Equal(
            ["m-1", "m-2"], new DeadMessageQuery { Location = Receiving }.Apply(all).Select(Id));
        Assert.Equal(
            ["m-2"],
            new DeadMessageQuery { Location = Receiving, Message = "m-2" }.Apply(all).Select(Id));
        Assert.Equal(
            ["m-2", "m-1", "m-3"],
            new DeadMessageQuery { Sort = "declines", Order = "descending" }.Apply(all)
                .Select(Id));
        Assert.Equal(
            ["m-3"], new DeadMessageQuery { Pattern = $"*/{Processor}" }.Apply(all).Select(Id));
        Assert.Equal(
            ["m-1"], new DeadMessageQuery { Pattern = "*/dead-message/m-1" }.Apply(all).Select(Id));
        Assert.Equal(Cluster.Name, DeadMessageQuery.Cluster(all[0]));
        Assert.Equal(Receiver, DeadMessageQuery.NodeName(all[0]));

        ArgumentException refused = Assert.Throws<ArgumentException>(
            () => new DeadMessageQuery { Sort = "mood" }.Apply(all));
        Assert.StartsWith("REFUSED", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReplayIsTheOneActAndAWordTheRuntimeTakes()
    {
        Assert.Equal(["Replay"], Enum.GetNames<DeadMessageAct>());

        XmipStatus status = RuntimeLibrary.Rules.DeadMessages.Replay(
            Receiving, "m-1", "ilian", out string said);

        Assert.True(status == XmipStatus.NotFound, $"{status} {said}");
        Assert.StartsWith("REFUSED", said, StringComparison.Ordinal);
        Assert.Empty(RuntimeLibrary.Rules.DeadMessages.Standing(Receiving).DeadMessages);
    }

    [Fact]
    public void TheGateRefusesAnObserverAndAnUnprovenCallerAndTakesAnOperatorAsProven()
    {
        ProgramAudit audit = new("Xmip.Surface.Test", Audit);
        SnapshotOperator inner = Over(Orders);
        DeadMessageRecord kept = inner.DeadMessages().DeadMessages[0];

        DeadMessageOperation observed = new GatedOperator(inner, Role.Observer, "operator", audit)
            .Act(kept, DeadMessageAct.Replay, Claimed);
        DeadMessageOperation unproven = new GatedOperator(inner, Role.Operator, null, audit)
            .Act(kept, DeadMessageAct.Replay, Claimed);

        Assert.All([observed, unproven], refused =>
        {
            Assert.False(refused.Applied);
            Assert.StartsWith("REFUSED. ", refused.Result, StringComparison.Ordinal);
        });
        Assert.False(Directory.Exists(Orders));

        DeadMessageOperation taken = new GatedOperator(inner, Role.Operator, "operator", audit)
            .Act(kept, DeadMessageAct.Replay, Claimed);

        Assert.True(taken.Applied, taken.Result);
        string order = File.ReadAllText(
            Assert.Single(Directory.GetFiles(Orders, "*.toml", SearchOption.AllDirectories)));
        Assert.Contains("operator", order, StringComparison.Ordinal);
        Assert.DoesNotContain(Claimed, order, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AReplayOverTheHubListsAndActsAsTheHostsSurfaceDoes()
    {
        SnapshotOperator local = Over(Orders);
        await using WebApplication host = await TestHost.Serve(
            local, role: Role.Operator, audit: Audit).ConfigureAwait(true);
        using RemoteOperator remote = new(new Uri(host.Urls.First()));
        IOperatorSurface surface = remote;

        Assert.True(remote.Connect(), remote.Reason);
        DeadMessageList listed = surface.DeadMessages();
        Assert.Equal(3, listed.DeadMessages.Count);
        DeadMessageRecord invoice = listed.DeadMessages.Single(entry => entry.Message == "m-2");
        Assert.Equal(local.DeadMessages().DeadMessages[1].Declines, invoice.Declines);

        DeadMessageOperation replayed = surface.Act(invoice, DeadMessageAct.Replay, Claimed);

        Assert.True(replayed.Applied, replayed.Result);
        string order = File.ReadAllText(
            Assert.Single(Directory.GetFiles(Orders, "*.toml", SearchOption.AllDirectories)));
        Assert.DoesNotContain(Claimed, order, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(scratch, recursive: true);
        }
        catch (IOException)
        {
            // A file the platform still holds; the temp directory keeps it.
        }
    }

    private static string Id(DeadMessageRecord entry)
    {
        return entry.Message;
    }

    // Two entries on the receiving node and one on the processing node; the
    // second declined twice, the first once, the third not asked.
    private SnapshotOperator Over(string orders)
    {
        string file = Path.Combine(scratch, $"snapshot-{Guid.NewGuid():N}.toml");
        File.WriteAllText(
            file,
            $"node = \"{Cluster.Scope}\"\n"
            + (orders.Length > 0 ? $"orders = '{orders}'\n" : string.Empty)
            + Entry(Receiver, "m-1", 1, 1_000, "validation = [[\"schema\", \"valid\"]]\n"
                + "declines = [[\"structured\", \"no MessageType\"]]\n")
            + Entry(Receiver, "m-2", 2, 2_000,
                "validation = [[\"schema\", \"valid\"], [\"signature\", \"valid\"]]\n"
                + "promoted = [[\"MessageType\", \"Invoice\"]]\n"
                + "declines = [[\"structured\", \"MessageType is Invoice, not json\"], "
                + "[\"edi\", \"paused\"]]\n")
            + Entry(Processor, "m-3", 1, 3_000, string.Empty));

        return new SnapshotOperator(file);
    }

    private static string Entry(string node, string message, int sequence, long millis, string rest)
    {
        return $"[[dead_messages]]\nnode = \"{Cluster.Scope}/node/{node}\"\n"
            + $"message = \"{message}\"\nsequence = {sequence}\nlocation = \"orders\"\n"
            + $"received_unix_nanos = {millis * 1_000_000}\n{rest}";
    }
}
