using Xmip.Abi.Operate;

namespace Xmip.Surface.Test;

/// <summary>
/// The Dead Message Queues as every surface lists them and replays from them
/// (ADR-0052, amendment 2026-10-01): a snapshot lists what its publication
/// carries — each entry's gate verdicts, promoted properties and declines in
/// the order written — and declines the Replay, which is asked at a node's
/// door (ADR-0014, amendment 2026-11-13); the one query drills, filters and
/// sorts; Replay is the one act and a word the runtime takes; and a Replay
/// from elsewhere passes the one role check, taken as the proven caller and
/// never the name given (ADR-0009, amendment 2026-10-03).
/// </summary>
public sealed class DeadMessageSurfaceTest : IDisposable
{
    // The caller the runtime proved, this process's account, as it names it.
    private static readonly string Me = RoleAssignment.ProcessCaller()?.Name ?? "nobody";

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

    private string Audit => Path.Combine(scratch, "audit");

    [Fact]
    public void ASnapshotListsWhatItsPublicationCarriesAndDeclinesTheReplay()
    {
        SnapshotOperator surface = Over();

        DeadMessageList listed = surface.DeadMessages();
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

        DeadMessageOperation declined =
            surface.Act(invoice, DeadMessageAct.Replay, "ilian", caller: null);

        Assert.False(declined.Applied);
        Assert.Equal(ActOutcome.Refused, declined.Outcome);
        Assert.Equal(SnapshotOperator.NotActed, declined.Result);
    }

    [Fact]
    public void TheQueryDrillsFiltersAndSorts()
    {
        IReadOnlyList<DeadMessageRecord> all = Over().DeadMessages().DeadMessages;

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
        Assert.Equal("replay", DeadMessageOperation.Word(DeadMessageAct.Replay));
        Assert.Empty(RuntimeLibrary.Rules.DeadMessages.Standing(Receiving).DeadMessages);
    }

    [Fact]
    public void TheGateRefusesAnObserverAndAnUnprovenCallerAndTakesAnOperatorAsProven()
    {
        ProgramAudit audit = new("Xmip.Surface.Test", Audit);
        SnapshotOperator inner = Over();
        DeadMessageRecord kept = inner.DeadMessages().DeadMessages[0];

        DeadMessageOperation observed = new GatedOperator(
                inner, new RoleContext(Role.Observer, RoleAssignment.ProcessCaller()), audit)
            .Act(kept, DeadMessageAct.Replay, Claimed, caller: null);
        DeadMessageOperation unproven = new GatedOperator(
                inner, new RoleContext(Role.Operator, null), audit)
            .Act(kept, DeadMessageAct.Replay, Claimed, caller: null);

        Assert.All([observed, unproven], refused =>
        {
            Assert.False(refused.Applied);
            Assert.StartsWith("REFUSED. ", refused.Result, StringComparison.Ordinal);
        });

        // Admitted, the act reaches the surface beneath the gate - a
        // snapshot, which declines it in its own words - as the proven
        // caller, audited so.
        DeadMessageOperation taken = new GatedOperator(
                inner, new RoleContext(Role.Operator, RoleAssignment.ProcessCaller()), audit)
            .Act(kept, DeadMessageAct.Replay, Claimed, caller: null);

        Assert.Equal(SnapshotOperator.NotActed, taken.Result);
        IReadOnlyList<AuditEntry> audited = audit
            .Read(new AuditQuery { Order = "ascending", IncludeHidden = true }).Records;
        Assert.Contains(audited, record => record.Message?.StartsWith(
            $"{Me} asked to replay", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(audited, record => record.Message?.Contains(
            Claimed, StringComparison.Ordinal) == true
            || record.Properties.Values.Contains(Claimed));
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
    private SnapshotOperator Over()
    {
        string file = Path.Combine(scratch, $"snapshot-{Guid.NewGuid():N}.xmip");
        File.WriteAllText(
            file,
            $"node = \"{Cluster.Scope}\"\n"
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
