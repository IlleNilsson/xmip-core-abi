using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xmip.Surface.Relay;

namespace Xmip.Surface.Test;

/// <summary>
/// A web host serving a surface's hub on a loopback port, hosted the way the
/// web host hosts it, for the tests that follow or act through one.
/// </summary>
internal static class TestHost
{
    /// <summary>
    /// A web host over <paramref name="surface"/>, on a loopback port of the
    /// system's choosing: plain where <paramref name="tls"/> is not given —
    /// loopback, the one exception — and HTTPS with it, as the web host binds
    /// it. It runs as <paramref name="role"/>, Observer where none is given,
    /// and audits into <paramref name="audit"/>, a directory of its own where
    /// none is given. At <paramref name="url"/> where given: a host served
    /// again where one stopped.
    /// </summary>
    public static async Task<WebApplication> Serve(
        IOperatorSurface surface,
        SurfaceTls? tls = null,
        Action<string>? refused = null,
        Role role = Role.Observer,
        string? audit = null,
        string? url = null)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls(
            url ?? (tls is null ? "http://127.0.0.1:0" : "https://127.0.0.1:0"));
        builder.WebHost.UseXmipTls(tls ?? SurfaceTls.None, refused);
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton(surface);
        builder.Services.AddSingleton(new RoleAssignment(role, directory: null));
        builder.Services.AddSingleton(new ProgramAudit(
            "Xmip.Surface.Test",
            audit ?? Path.Combine(Path.GetTempPath(), $"xmip-hub-audit-{Guid.NewGuid():n}")));
        builder.Services.AddXmipSurfaceRelay();

        WebApplication host = builder.Build();
        host.MapXmipSurfaceHub(refused);
        await host.StartAsync().ConfigureAwait(false);

        return host;
    }

    /// <summary>
    /// A snapshot of <paramref name="cluster"/> holding one Subscription on its
    /// first node and one Event subscription on its last, written in
    /// <paramref name="directory"/>, whose publisher takes orders in
    /// <paramref name="orders"/>: what the tests that act through the gate act on.
    /// </summary>
    public static SnapshotOperator Acting(TestCluster cluster, string directory, string orders)
    {
        ArgumentNullException.ThrowIfNull(cluster);

        string snapshot = Path.Combine(directory, "snapshot.toml");
        File.WriteAllText(
            snapshot,
            $"node = \"{cluster.Scope}\"\norders = '{orders}'\n"
            + $"[[subscriptions]]\nnode = \"{cluster.NodeScope(0)}\"\nname = \"routed\"\n"
            + "state = \"active\"\n"
            + $"[[event_subscriptions]]\nnode = \"{cluster.NodeScope(cluster.Nodes.Count - 1)}\"\n"
            + "id = 7\nsubscriber = \"p\"\nstate = \"active\"\n");

        return new SnapshotOperator(snapshot);
    }

    /// <summary>What a host or a surface presents and trusts: a pair the
    /// authority issued, and its anchor.</summary>
    public static SurfaceTls Tls(TestAuthority authority, (string Certificate, string Key) pair)
    {
        return SurfaceTls.Load(pair.Certificate, pair.Key, authority.Anchor);
    }
}
