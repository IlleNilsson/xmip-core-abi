using Xmip.Abi.Operate;

namespace Xmip.Surface.Test;

/// <summary>
/// Retry and Dismiss on a Message that failed as every surface takes them
/// (runtime-model.md section 13; ADR-0013): a snapshot finds the node that
/// sends the Message's Send Port — named by the node or by the Send Port's
/// scope beneath it, where the node publishes the last Message that failed —
/// and declines the act, which is asked at a node's door (ADR-0014,
/// amendment 2026-11-13), and refuses where the scope is on no node; Retry
/// and Dismiss are the acts and words the runtime takes; and an act from
/// elsewhere passes the one role check, taken as the proven caller and never
/// the name given (ADR-0009, amendment 2026-10-03).
/// </summary>
public sealed class MessageSurfaceTest : IDisposable
{
    // The caller the runtime proved, this process's account, as it names it.
    private static readonly string Me = RoleAssignment.ProcessCaller()?.Name ?? "nobody";

    // The name a caller claims, which no act may carry.
    private const string Claimed = "claimed-by-the-caller";

    // A Journey's identifier as the node publishes it at its Send Port.
    private const string Journey = "01928f6e-7c1a-7d3e-9a4b-5c6d7e8f9a0b";

    private static readonly TestCluster Cluster = TestCluster.Read();

    // The test cluster's sending node, by what it declares, and a Send Port on it.
    private static readonly string Sender = Cluster.WithRole("sending");
    private static readonly string Sending = $"{Cluster.Scope}/node/{Sender}";
    private static readonly string Port = $"{Sending}/send/invoices";

    private readonly string scratch =
        Path.Combine(Path.GetTempPath(), $"xmip-journey-{Guid.NewGuid():N}");

    public MessageSurfaceTest()
    {
        Directory.CreateDirectory(scratch);
    }

    private string Audit => Path.Combine(scratch, "audit");

    [Theory]
    [InlineData(MessageAct.Retry)]
    [InlineData(MessageAct.Dismiss)]
    public void ASnapshotFindsTheNodeThatSendsThePortAndDeclinesTheAct(MessageAct act)
    {
        MessageOperation declined = Over().Act(Port, Journey, act, "ilian", caller: null);

        Assert.False(declined.Applied);
        Assert.Equal(Sending, declined.Node);
        Assert.Equal(SnapshotOperator.NotActed, declined.Result);
    }

    [Fact]
    public void ASnapshotRefusesWhereTheScopeIsOnNoNodeOrNoJourneyIsNamed()
    {
        MessageOperation nowhere = Over().Act(
            Cluster.Scope, Journey, MessageAct.Dismiss, "ilian", caller: null);
        MessageOperation unnamed = Over().Act(
            Port, string.Empty, MessageAct.Dismiss, "ilian", caller: null);

        Assert.All([nowhere, unnamed], refused =>
        {
            Assert.False(refused.Applied);
            Assert.StartsWith("REFUSED", refused.Result, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void RetryAndDismissAreTheActsAndWordsTheRuntimeTakes()
    {
        Assert.Equal(["Retry", "Dismiss"], Enum.GetNames<MessageAct>());
        Assert.Equal(
            ["retry", "dismiss"], Enum.GetValues<MessageAct>().Select(MessageOperation.Word));
        Assert.Equal(Sending, ScopeTree.NodeScope(Port));
        Assert.Equal(Sending, ScopeTree.NodeScope(Sending));
        Assert.Equal(string.Empty, ScopeTree.NodeScope(Cluster.Scope));
    }

    [Fact]
    public void TheLastMessageThatFailedIsReadFromTheSendPortsEvidence()
    {
        Assert.Equal(
            Journey,
            MessageOperation.FailedIn(
                $"started; tcp at 127.0.0.1:9; sent 4, failed 1, waiting 0; the Message of the "
                + $"Journey {Journey} failed: every Send Location failed its tries; the Message "
                + "of the Journey before"));
        Assert.Null(MessageOperation.FailedIn("started; sent 4, failed 0, waiting 0"));
        Assert.Null(MessageOperation.FailedIn(null));
    }

    [Fact]
    public void TheGateRefusesAnObserverAndAnUnprovenCallerAndTakesAnOperatorAsProven()
    {
        ProgramAudit audit = new("Xmip.Surface.Test", Audit);
        SnapshotOperator inner = Over();

        MessageOperation observed = new GatedOperator(
                inner, new RoleContext(Role.Observer, RoleAssignment.ProcessCaller()), audit)
            .Act(Port, Journey, MessageAct.Retry, Claimed, caller: null);
        MessageOperation unproven = new GatedOperator(
                inner, new RoleContext(Role.Operator, null), audit)
            .Act(Port, Journey, MessageAct.Dismiss, Claimed, caller: null);

        Assert.All([observed, unproven], refused =>
        {
            Assert.False(refused.Applied);
            Assert.StartsWith("REFUSED. ", refused.Result, StringComparison.Ordinal);
        });

        // Admitted, the act reaches the surface beneath the gate - a
        // snapshot, which declines it in its own words - as the proven
        // caller, audited so.
        MessageOperation taken = new GatedOperator(
                inner, new RoleContext(Role.Operator, RoleAssignment.ProcessCaller()), audit)
            .Act(Port, Journey, MessageAct.Retry, Claimed, caller: null);

        Assert.Equal(SnapshotOperator.NotActed, taken.Result);
        IReadOnlyList<AuditEntry> audited = audit
            .Read(new AuditQuery { Order = "ascending", IncludeHidden = true }).Records;
        Assert.Contains(audited, record => record.Message?.StartsWith(
            $"{Me} asked to retry", StringComparison.Ordinal) == true);
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

    // A publication of the test cluster.
    private SnapshotOperator Over()
    {
        string file = Path.Combine(scratch, $"snapshot-{Guid.NewGuid():N}.xmip");
        File.WriteAllText(file, $"node = \"{Cluster.Scope}\"\n");

        return new SnapshotOperator(file);
    }
}
