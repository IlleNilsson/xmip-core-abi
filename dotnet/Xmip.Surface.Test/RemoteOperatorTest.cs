using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xmip.Surface.Relay;

namespace Xmip.Surface.Test;

/// <summary>
/// The surface over a web host on another machine, proved against a real hub
/// on a loopback port hosted the way the web host hosts it: it answers what
/// the host's surface answers, it is told when that surface changes, and a
/// host that is not there is said so (ADR-0052, amendment 2026-09-15). Over
/// TLS both ends present a certificate and check the other's, and a
/// certificate the other end's anchors do not reach is refused (ADR-0063
/// clause 1).
/// </summary>
public sealed class RemoteOperatorTest
{
    [Fact]
    public async Task SaysWhatTheHostsRunWasStartedWith()
    {
        IOperatorSurface local = new SnapshotOperator(
            Path.Combine(AppContext.BaseDirectory, "Fixture", "cluster.toml"));
        await using WebApplication host = await Serve(local).ConfigureAwait(true);
        using RemoteOperator remote = new(new Uri(host.Urls.First()));

        Assert.True(remote.Connect(), remote.Reason);
        Assert.Equal(
            "RoundTrip · C1 · nodes alpha=receive beta=process+send gamma=send · "
            + "online alpha · realistic",
            remote.Run().Line());
        Assert.Equal(
            ["process", "send"], ((IOperatorSurface)remote).Capability("beta").Stages);
        Assert.Equal(
            local.Topology().Nodes.Select(node => node.Kind),
            remote.Topology().Nodes.Select(node => node.Kind));
    }

    [Fact]
    public async Task AnswersWhatTheHostReadsAndIsToldWhenItChanges()
    {
        string copy = Path.Combine(
            Path.GetTempPath(), $"xmip-remote-{Guid.NewGuid():n}-snapshot.toml");
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixture", "snapshot.toml"), copy);

        try
        {
            IOperatorSurface local = new SnapshotOperator(copy);
            await using WebApplication host = await Serve(local).ConfigureAwait(true);
            using RemoteOperator remoteHost = new(new Uri(host.Urls.First()));
            IOperatorSurface remote = remoteHost;

            Assert.True(remoteHost.Connect(), remoteHost.Reason);
            Assert.Equal($"REMOTE — {remoteHost.Host}", remote.Source);
            Assert.Equal(local.Health(ScopeTree.Root), remote.Health(ScopeTree.Root));
            Assert.Equal(
                local.Health("xmip:///edge-02").Select(record => record.Scope),
                remote.Health("xmip:///edge-02").Select(record => record.Scope));
            Assert.Equal(
                local.Figures(ScopeTree.Root) with { Observed = null },
                remote.Figures(ScopeTree.Root) with { Observed = null });
            Assert.Equal(local.Topology().Source, remote.Topology().Source);
            Assert.Equal(local.PauseScope("xmip:///edge-01", "test"),
                remote.PauseScope("xmip:///edge-01", "test"));

            using CancellationTokenSource patience = new(TimeSpan.FromSeconds(30));
            await using IAsyncEnumerator<SurfaceChange> feed =
                remote.WatchAsync(patience.Token).GetAsyncEnumerator(patience.Token);

            Assert.True(await feed.MoveNextAsync().ConfigureAwait(true));
            Assert.Equal(remote.Source, feed.Current.Source);

            // Told, not asked: the host's file advances, the host's feed fires,
            // the relay pushes, and the notice carries the host's own source.
            File.WriteAllText(copy, File.ReadAllText(copy) + "\n# advanced\n");

            SurfaceChange told;

            do
            {
                Assert.True(await feed.MoveNextAsync().ConfigureAwait(true));
                told = feed.Current;
            }
            while (told.Source != local.Source);

            Assert.Equal(local.Source, told.Source);
        }
        finally
        {
            File.Delete(copy);
        }
    }

    [Fact]
    public void AHostThatIsNotThereIsSaidSo()
    {
        using RemoteOperator remote = new(new Uri("http://127.0.0.1:9"));

        Assert.False(remote.Connect());
        Assert.NotEmpty(remote.Reason);
        Assert.StartsWith("REMOTE — http://127.0.0.1:9/ unreachable: ", remote.Source,
            StringComparison.Ordinal);
        Assert.Empty(remote.Health(ScopeTree.Root));
        Assert.Null(remote.Measure(ScopeTree.Root, Abi.Operate.Counted.Streams));
    }

    [Fact]
    public void TheHubIsWhereTheClientLooks()
    {
        using RemoteOperator remote = new(new Uri("http://host:5087"));

        Assert.Equal(new Uri("http://host:5087/surface"), remote.Hub);
        Assert.Equal(RemoteOperator.HubPath, SurfaceHub.Path);
    }

    [Fact]
    public async Task AnswersOverMutualTlsWithCertificatesBothEndsTrust()
    {
        using TestAuthority authority = new("mutual");
        SnapshotOperator local = Fixture();
        List<string> refused = [];
        await using WebApplication host =
            await Serve(local, Tls(authority, authority.Server()), refused.Add)
                .ConfigureAwait(true);
        using RemoteOperator remote = new(
            new Uri(host.Urls.First()), Tls(authority, authority.Client()));

        Assert.StartsWith("https://127.0.0.1:", host.Urls.First(), StringComparison.Ordinal);
        Assert.True(remote.Connect(), remote.Reason);
        Assert.Equal(local.Health(ScopeTree.Root), ((IOperatorSurface)remote).Health(ScopeTree.Root));
        Assert.Empty(refused);
    }

    [Fact]
    public async Task AClientCertificateTheHostDoesNotTrustIsRefused()
    {
        using TestAuthority authority = new("host");
        using TestAuthority stranger = new("stranger");
        SnapshotOperator local = Fixture();
        List<string> refused = [];
        await using WebApplication host =
            await Serve(local, Tls(authority, authority.Server()), refused.Add)
                .ConfigureAwait(true);

        // Trusts the host, and presents a certificate another authority issued.
        (string certificate, string privateKey) = stranger.Client();
        SurfaceTls wrong = SurfaceTls.Load(certificate, privateKey, authority.Anchor);
        using RemoteOperator remote = new(new Uri(host.Urls.First()), wrong);

        Assert.False(remote.Connect());
        Assert.Contains(refused, why => why.Contains("xmip-client", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AHostCertificateTheSurfaceDoesNotTrustIsRefused()
    {
        using TestAuthority authority = new("served");
        using TestAuthority stranger = new("expected");
        SnapshotOperator local = Fixture();
        await using WebApplication host =
            await Serve(local, Tls(authority, authority.Server())).ConfigureAwait(true);
        (string certificate, string privateKey) = authority.Client();
        using RemoteOperator remote = new(
            new Uri(host.Urls.First()),
            SurfaceTls.Load(certificate, privateKey, stranger.Anchor));

        Assert.False(remote.Connect());
        Assert.StartsWith("REFUSED. CN=xmip-server is not trusted", remote.Reason,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task NoCertificateOverTlsIsRefusedAtTheHub()
    {
        using TestAuthority authority = new("bare");
        SnapshotOperator local = Fixture();
        List<string> refused = [];
        await using WebApplication host =
            await Serve(local, Tls(authority, authority.Server()), refused.Add)
                .ConfigureAwait(true);
        using RemoteOperator remote = new(
            new Uri(host.Urls.First()), SurfaceTls.Load(null, null, authority.Anchor));

        Assert.False(remote.Connect());
        Assert.Contains(refused, why => why.Contains("without a certificate", StringComparison.Ordinal));
    }

    [Fact]
    public void PlainHttpBeyondThisMachineIsRefusedBeforeAnyConnection()
    {
        using RemoteOperator remote = new(new Uri("http://192.0.2.1:5087"));

        Assert.False(remote.Connect());
        Assert.StartsWith("REFUSED. 192.0.2.1 is plain http beyond this machine", remote.Reason,
            StringComparison.Ordinal);
    }

    /// <summary>The snapshot fixture, as the surface a host serves.</summary>
    private static SnapshotOperator Fixture()
    {
        return new SnapshotOperator(
            Path.Combine(AppContext.BaseDirectory, "Fixture", "snapshot.toml"));
    }

    /// <summary>What a host or a surface presents and trusts: a pair the
    /// authority issued, and its anchor.</summary>
    private static SurfaceTls Tls(TestAuthority authority, (string Certificate, string Key) pair)
    {
        return SurfaceTls.Load(pair.Certificate, pair.Key, authority.Anchor);
    }

    /// <summary>A web host over <paramref name="surface"/>, serving the hub
    /// on a loopback port of the system's choosing: plain where
    /// <paramref name="tls"/> is not given — loopback, the one exception —
    /// and HTTPS with it, as the web host binds it.</summary>
    private static async Task<WebApplication> Serve(
        IOperatorSurface surface, SurfaceTls? tls = null, Action<string>? refused = null)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls(tls is null ? "http://127.0.0.1:0" : "https://127.0.0.1:0");
        builder.WebHost.UseXmipTls(tls ?? SurfaceTls.None, refused);
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton(surface);
        builder.Services.AddXmipSurfaceRelay();

        WebApplication host = builder.Build();
        host.MapXmipSurfaceHub(refused);
        await host.StartAsync().ConfigureAwait(false);

        return host;
    }
}
