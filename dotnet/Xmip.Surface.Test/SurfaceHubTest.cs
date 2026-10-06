using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Xmip.Abi.Operate;

namespace Xmip.Surface.Test;

/// <summary>
/// An act that reaches a host over its surface hub is refused server-side
/// unless the role the host grants its caller may act, and is taken as the
/// identity the client certificate proved, never the name the caller gives
/// (ADR-0009, amendments 2026-10-03 and 2026-10-06); taken or refused, it is audited (ADR-0062). Proved against a
/// real hub on a loopback port, over a snapshot that leaves its orders in a
/// directory, with the test cluster's names.
/// </summary>
public sealed partial class SurfaceHubTest : IDisposable
{
    // The name a caller claims, which no act may carry.
    private const string Claimed = "claimed-by-the-caller";

    private readonly TestCluster cluster = TestCluster.Read();

    private readonly string scratch =
        Path.Combine(Path.GetTempPath(), $"xmip-hub-{Guid.NewGuid():n}");

    public SurfaceHubTest()
    {
        Directory.CreateDirectory(scratch);
    }

    private string Orders => Path.Combine(scratch, "orders");

    private string Audit => Path.Combine(scratch, "audit");

    [Fact]
    public async Task AnOperatorHostTakesEachActAsTheCertificatesSubject()
    {
        using TestAuthority authority = new("operator");
        (string certificate, string key) = authority.Client();
        string subject = X509Certificate2.CreateFromPemFile(certificate, key).Subject;
        SnapshotOperator local = Surface();
        await using WebApplication host = await TestHost.Serve(
            local, TestHost.Tls(authority, authority.Server()), role: Role.Operator, audit: Audit)
            .ConfigureAwait(true);
        using RemoteOperator remote = new(
            new Uri(host.Urls.First()), TestHost.Tls(authority, (certificate, key)));
        IOperatorSurface surface = remote;

        Assert.True(remote.Connect(), remote.Reason);
        SubscriptionOperation paused = surface.Act(
            Assert.Single(surface.Subscriptions().Subscriptions), SubscriptionAct.Pause, Claimed);
        EventSubscriptionOperation removed = surface.Act(
            Assert.Single(surface.EventSubscriptions().EventSubscriptions),
            EventSubscriptionAct.Remove,
            Claimed);

        Assert.True(paused.Applied, paused.Result);
        Assert.True(removed.Applied, removed.Result);
        Assert.All(Enum.GetValues<ScopeAction>(), action => Assert.Equal(
            local.Control(cluster.Scope, action, Claimed),
            surface.Control(cluster.Scope, action, Claimed)));

        string[] orders = Directory.GetFiles(Orders, "*.toml", SearchOption.AllDirectories);
        Assert.Equal(2, orders.Length);
        Assert.All(orders, order =>
        {
            string text = File.ReadAllText(order);
            Assert.Contains(subject, text, StringComparison.Ordinal);
            Assert.DoesNotContain(Claimed, text, StringComparison.Ordinal);
        });

        string audited = File.ReadAllText(Path.Combine(Audit, "audit.toml"));
        Assert.Equal(4, Acts().Count(audited));
        Assert.Contains(subject, audited, StringComparison.Ordinal);
        Assert.DoesNotContain(Claimed, audited, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnObserverHostRefusesEveryActInWordsAndAuditsIt()
    {
        using TestAuthority authority = new("observer");
        SnapshotOperator local = Surface();
        await using WebApplication host = await TestHost.Serve(
            local, TestHost.Tls(authority, authority.Server()), role: Role.Observer, audit: Audit)
            .ConfigureAwait(true);
        using RemoteOperator remote = new(
            new Uri(host.Urls.First()), TestHost.Tls(authority, authority.Client()));

        Assert.True(remote.Connect(), remote.Reason);
        string[] said = Every(remote);

        Assert.All(said, refused =>
        {
            Assert.StartsWith("REFUSED. ", refused, StringComparison.Ordinal);
            Assert.Contains($" is {Role.Observer}, ", refused, StringComparison.Ordinal);
        });
        Assert.False(Directory.Exists(Orders));
        Assert.Equal(
            said.Length, Acts().Count(File.ReadAllText(Path.Combine(Audit, "audit.toml"))));
    }

    [Fact]
    public async Task OnLoopbackWithoutACertificateTheActIsTheHostsOwnUser()
    {
        // Only someone logged on to the machine reaches loopback, so the act
        // is the operating system user the host runs as (the owner,
        // 2026-10-03), never the name the caller gives.
        await using WebApplication host = await TestHost.Serve(
            Surface(), role: Role.Operator, audit: Audit).ConfigureAwait(true);
        using RemoteOperator remote = new(new Uri(host.Urls.First()));
        IOperatorSurface surface = remote;

        Assert.True(remote.Connect(), remote.Reason);
        SubscriptionOperation paused = surface.Act(
            Assert.Single(surface.Subscriptions().Subscriptions), SubscriptionAct.Pause, Claimed);

        Assert.True(paused.Applied, paused.Result);
        string order = Assert.Single(
            Directory.GetFiles(Orders, "*.toml", SearchOption.AllDirectories));
        string audited = File.ReadAllText(Path.Combine(Audit, "audit.toml"));
        Assert.All([File.ReadAllText(order), audited], text =>
        {
            Assert.True(Names(text, GatedOperator.HostUser), text);
            Assert.DoesNotContain(Claimed, text, StringComparison.Ordinal);
        });
        Assert.Equal(1, Acts().Count(audited));
    }

    [Fact]
    public async Task AScopeActOverTheHubSaysWhatTheHostDidNotWhatWasAsked()
    {
        // Until 2026-10-06 a remote pause was said applied whatever the host
        // did: the host's words came back and the remote took them as done.
        await using WebApplication host = await TestHost.Serve(
            new Applying(), role: Role.Operator, audit: Audit).ConfigureAwait(true);
        using RemoteOperator remote = new(new Uri(host.Urls.First()));
        IOperatorSurface surface = remote;

        Assert.True(remote.Connect(), remote.Reason);
        ScopeOperation paused = surface.Control(cluster.Scope, ScopeAction.Pause, Claimed);
        ScopeOperation resumed = surface.Control(cluster.Scope, ScopeAction.Resume, Claimed);

        Assert.True(paused.Applied, paused.Result);
        Assert.Equal($"paused {cluster.Scope} by {GatedOperator.HostUser}", paused.Result);
        Assert.True(resumed.Applied, resumed.Result);
        Assert.Equal(paused.Result, surface.PauseScope(cluster.Scope, Claimed));
    }

    [Fact]
    public void FromElsewhereWithoutACertificateNoActIsTaken()
    {
        string? proven = GatedOperator.Proven(null, loopback: false);
        GatedOperator gated = new(
            Surface(),
            new RoleContext(Role.Operator, proven),
            new ProgramAudit("Xmip.Surface.Test", Audit));

        Assert.Null(proven);
        Assert.Equal(GatedOperator.HostUser, GatedOperator.Proven(null, loopback: true));
        Assert.All(Every(gated), refused =>
            Assert.StartsWith(
                "REFUSED. Nothing proved who asks", refused, StringComparison.Ordinal));
        Assert.False(Directory.Exists(Orders));
    }

    // Whether a TOML text names who, as written or as a basic string escapes
    // its backslash.
    private static bool Names(string text, string who)
    {
        return text.Contains(who, StringComparison.Ordinal)
            || text.Contains(who.Replace("\\", "\\\\", StringComparison.Ordinal),
                StringComparison.Ordinal);
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

    // Every act a remote surface can ask a host for, each once, and what came
    // of it.
    private string[] Every(IOperatorSurface remote)
    {
        SubscriptionRecord subscription = Assert.Single(remote.Subscriptions().Subscriptions);
        EventSubscriptionRecord held = Assert.Single(
            remote.EventSubscriptions().EventSubscriptions);

        return
        [
            .. Enum.GetValues<SubscriptionAct>().Select(act =>
                remote.Act(subscription, act, Claimed)).Select(Refused),
            .. Enum.GetValues<EventSubscriptionAct>().Select(act =>
                remote.Act(held, act, Claimed)).Select(Refused),
            .. Enum.GetValues<ScopeAction>().Select(action =>
                remote.Control(cluster.Scope, action, Claimed)).Select(Refused),
        ];
    }

    private static string Refused(ScopeOperation operation)
    {
        Assert.False(operation.Applied);
        return operation.Result;
    }

    private static string Refused(SubscriptionOperation operation)
    {
        Assert.False(operation.Applied);
        return operation.Result;
    }

    private static string Refused(EventSubscriptionOperation operation)
    {
        Assert.False(operation.Applied);
        return operation.Result;
    }

    // A snapshot of the test cluster whose publisher takes orders in this
    // test's directory.
    private SnapshotOperator Surface()
    {
        return TestHost.Acting(cluster, scratch, Orders);
    }

    [GeneratedRegex("^action = \"act\"$", RegexOptions.Multiline)]
    private static partial Regex Acts();

    // A host's surface that applies every scope act it is handed, saying
    // whose it was, as a runtime in its process does.
    private sealed class Applying : IOperatorSurface
    {
        public string Source => "applying";

        public IReadOnlyList<HealthRecord> Health(string scope)
        {
            return [];
        }

        public MeasurementRecord? Measure(string scope, Counted counted)
        {
            return null;
        }

        public string PauseScope(string scope, string who)
        {
            return $"paused {scope} by {who}";
        }

        public string ResumeScope(string scope)
        {
            return $"resumed {scope}";
        }
    }
}
