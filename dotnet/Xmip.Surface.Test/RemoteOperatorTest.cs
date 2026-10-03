using Microsoft.AspNetCore.Builder;
using Xmip.Surface.Relay;

namespace Xmip.Surface.Test;

/// <summary>
/// The surface over a web host on another machine, proved against a real hub
/// on a loopback port hosted the way the web host hosts it: it answers what
/// the host's surface answers, it is told when that surface changes, and a
/// host that is not there is said so (ADR-0052, amendment 2026-09-15). Over
/// TLS both ends present a certificate and check the other's, and a
/// certificate the other end's anchors do not reach is refused (ADR-0063
/// clause 1). What an act across the hub comes to is <see cref="SurfaceHubTest"/>'s.
/// </summary>
public sealed class RemoteOperatorTest
{
    private static readonly TestCluster Cluster = TestCluster.Read();

    [Fact]
    public async Task SaysWhatTheHostsRunWasStartedWith()
    {
        IOperatorSurface local = new SnapshotOperator(
            Path.Combine(AppContext.BaseDirectory, "Fixture", "cluster.toml"));
        await using WebApplication host = await TestHost.Serve(local).ConfigureAwait(true);
        using RemoteOperator remote = new(new Uri(host.Urls.First()));
        string receiver = Cluster.WithRole("receiving");
        string processor = Cluster.WithRole("processing");

        Assert.True(remote.Connect(), remote.Reason);
        Assert.Equal(
            $"RoundTrip · {Cluster.Name} · nodes {receiver}=receiving "
            + $"{processor}=processing+sending {Cluster.WithRole("sending")}=sending · "
            + $"online {receiver} · realistic",
            remote.Run().Line());
        Assert.Equal(
            ["processing", "sending"], ((IOperatorSurface)remote).Capability(processor).Roles);
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
            await using WebApplication host = await TestHost.Serve(local).ConfigureAwait(true);
            using RemoteOperator remoteHost = new(new Uri(host.Urls.First()));
            IOperatorSurface remote = remoteHost;

            Assert.True(remoteHost.Connect(), remoteHost.Reason);
            Assert.Equal($"REMOTE — {remoteHost.Host}", remote.Source);
            Assert.Equal(local.Health(ScopeTree.Root), remote.Health(ScopeTree.Root));
            string sending = ScopeTree.Root + Cluster.WithRole("sending");
            Assert.Equal(
                local.Health(sending).Select(record => record.Scope),
                remote.Health(sending).Select(record => record.Scope));
            Assert.Equal(
                local.Figures(ScopeTree.Root) with { Observed = null },
                remote.Figures(ScopeTree.Root) with { Observed = null });
            Assert.Equal(local.Topology().Source, remote.Topology().Source);

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
            await TestHost.Serve(
                    local, TestHost.Tls(authority, authority.Server()), refused.Add)
                .ConfigureAwait(true);
        using RemoteOperator remote = new(
            new Uri(host.Urls.First()), TestHost.Tls(authority, authority.Client()));

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
            await TestHost.Serve(
                    local, TestHost.Tls(authority, authority.Server()), refused.Add)
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
            await TestHost.Serve(local, TestHost.Tls(authority, authority.Server()))
                .ConfigureAwait(true);
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
            await TestHost.Serve(
                    local, TestHost.Tls(authority, authority.Server()), refused.Add)
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
}
