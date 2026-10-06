using Microsoft.AspNetCore.Builder;
using Xmip.Abi.Module;

namespace Xmip.Surface.Test;

/// <summary>
/// Retry and Dismiss on a Journey that failed as every surface takes them
/// (runtime-model.md section 13; ADR-0013): a snapshot leaves the act for the
/// node that sends the Journey's Send Port — named by the node or by the
/// Send Port's scope beneath it, where the node publishes the last Journey
/// that failed — where its publication says its publisher takes orders, and
/// declines where it says nowhere or the scope is on no node; Retry and
/// Dismiss are the acts and words the runtime takes; and an act from
/// elsewhere passes the one role check, taken as the proven caller and never
/// the name given (ADR-0009, amendment 2026-10-03).
/// </summary>
public sealed class JourneySurfaceTest : IDisposable
{
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

    public JourneySurfaceTest()
    {
        Directory.CreateDirectory(scratch);
    }

    private string Orders => Path.Combine(scratch, "orders");

    private string Audit => Path.Combine(scratch, "audit");

    [Theory]
    [InlineData(JourneyAct.Retry)]
    [InlineData(JourneyAct.Dismiss)]
    public void ASnapshotLeavesTheActForTheNodeThatSendsThePort(JourneyAct act)
    {
        JourneyOperation done = Over(Orders).Act(Port, Journey, act, "ilian");

        Assert.True(done.Applied, done.Result);
        Assert.Equal(Sending, done.Node);
        Assert.Equal(
            $"{JourneyOperation.Word(act)} of the Journey {Journey} left for {Sender} "
            + "to take at its next look",
            done.Result);
        string order = File.ReadAllText(
            Assert.Single(Directory.GetFiles(Path.Combine(Orders, Sender), "*.toml")));
        Assert.Contains(JourneyOperation.Noun, order, StringComparison.Ordinal);
        Assert.Contains(Journey, order, StringComparison.Ordinal);
    }

    [Fact]
    public void ASnapshotDeclinesWhereItsPublisherTakesNoOrdersOrTheScopeIsOnNoNode()
    {
        JourneyOperation untaken = Over(string.Empty).Act(Port, Journey, JourneyAct.Retry, "ilian");
        JourneyOperation nowhere = Over(Orders).Act(
            Cluster.Scope, Journey, JourneyAct.Dismiss, "ilian");
        JourneyOperation unnamed = Over(Orders).Act(
            Port, string.Empty, JourneyAct.Dismiss, "ilian");

        Assert.False(untaken.Applied);
        Assert.Contains("takes no orders", untaken.Result, StringComparison.Ordinal);
        Assert.All([nowhere, unnamed], refused =>
        {
            Assert.False(refused.Applied);
            Assert.StartsWith("REFUSED", refused.Result, StringComparison.Ordinal);
        });
        Assert.False(Directory.Exists(Orders));
    }

    [Fact]
    public void RetryAndDismissAreTheActsAndWordsTheRuntimeTakes()
    {
        Assert.Equal(["Retry", "Dismiss"], Enum.GetNames<JourneyAct>());
        Assert.Equal(Sending, ScopeTree.NodeScope(Port));
        Assert.Equal(Sending, ScopeTree.NodeScope(Sending));
        Assert.Equal(string.Empty, ScopeTree.NodeScope(Cluster.Scope));

        foreach (JourneyAct act in Enum.GetValues<JourneyAct>())
        {
            XmipStatus status = RuntimeLibrary.Rules.Journeys.Act(
                Sending, Journey, JourneyOperation.Word(act), "ilian", out string said);

            Assert.True(status == XmipStatus.NotFound, $"{status} {said}");
            Assert.StartsWith("REFUSED", said, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void TheLastJourneyThatFailedIsReadFromTheSendPortsEvidence()
    {
        Assert.Equal(
            Journey,
            JourneyOperation.FailedIn(
                $"started; tcp at 127.0.0.1:9; sent 4, failed 1, waiting 0; the Journey "
                + $"{Journey} failed: every Send Location failed its tries; the Journey before"));
        Assert.Null(JourneyOperation.FailedIn("started; sent 4, failed 0, waiting 0"));
        Assert.Null(JourneyOperation.FailedIn(null));
    }

    [Fact]
    public void TheGateRefusesAnObserverAndAnUnprovenCallerAndTakesAnOperatorAsProven()
    {
        ProgramAudit audit = new("Xmip.Surface.Test", Audit);
        SnapshotOperator inner = Over(Orders);

        JourneyOperation observed = new GatedOperator(
                inner, new RoleContext(Role.Observer, "operator"), audit)
            .Act(Port, Journey, JourneyAct.Retry, Claimed);
        JourneyOperation unproven = new GatedOperator(
                inner, new RoleContext(Role.Operator, null), audit)
            .Act(Port, Journey, JourneyAct.Dismiss, Claimed);

        Assert.All([observed, unproven], refused =>
        {
            Assert.False(refused.Applied);
            Assert.StartsWith("REFUSED. ", refused.Result, StringComparison.Ordinal);
        });
        Assert.False(Directory.Exists(Orders));

        JourneyOperation taken = new GatedOperator(
                inner, new RoleContext(Role.Operator, "operator"), audit)
            .Act(Port, Journey, JourneyAct.Retry, Claimed);

        Assert.True(taken.Applied, taken.Result);
        string order = File.ReadAllText(
            Assert.Single(Directory.GetFiles(Orders, "*.toml", SearchOption.AllDirectories)));
        Assert.Contains("operator", order, StringComparison.Ordinal);
        Assert.DoesNotContain(Claimed, order, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnActOverTheHubIsTakenAsTheHostsSurfaceTakesIt()
    {
        SnapshotOperator local = Over(Orders);
        await using WebApplication host = await TestHost.Serve(
            local, role: Role.Operator, audit: Audit).ConfigureAwait(true);
        using RemoteOperator remote = new(new Uri(host.Urls.First()));
        IOperatorSurface surface = remote;

        Assert.True(remote.Connect(), remote.Reason);

        JourneyOperation dismissed = surface.Act(Port, Journey, JourneyAct.Dismiss, Claimed);

        Assert.True(dismissed.Applied, dismissed.Result);
        Assert.Equal(Sending, dismissed.Node);
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

    // A publication of the test cluster, its orders where given.
    private SnapshotOperator Over(string orders)
    {
        string file = Path.Combine(scratch, $"snapshot-{Guid.NewGuid():N}.toml");
        File.WriteAllText(
            file,
            $"node = \"{Cluster.Scope}\"\n"
            + (orders.Length > 0 ? $"orders = '{orders}'\n" : string.Empty));

        return new SnapshotOperator(file);
    }
}
