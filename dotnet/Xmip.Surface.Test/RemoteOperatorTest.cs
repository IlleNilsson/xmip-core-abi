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
/// host that is not there is said so (ADR-0052, amendment 2026-09-15).
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
            "RoundTrip · C1 · nodes R1=receive P1=process+send S1=send · online R1 · realistic",
            remote.Run().Line());
        Assert.Equal(
            ["process", "send"], ((IOperatorSurface)remote).Capability("P1").Stages);
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

    /// <summary>A web host over <paramref name="surface"/>, serving the hub
    /// on a loopback port of the system's choosing.</summary>
    private static async Task<WebApplication> Serve(IOperatorSurface surface)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton(surface);
        builder.Services.AddXmipSurfaceRelay();

        WebApplication host = builder.Build();
        host.MapXmipSurfaceHub();
        await host.StartAsync().ConfigureAwait(false);

        return host;
    }
}
