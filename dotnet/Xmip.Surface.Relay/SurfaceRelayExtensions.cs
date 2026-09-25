using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Xmip.Surface.Relay;

/// <summary>How a web host serves its surface: one call to register, one to map.</summary>
public static class SurfaceRelayExtensions
{
    /// <summary>SignalR and the relay that pushes the host's change feed.</summary>
    public static IServiceCollection AddXmipSurfaceRelay(this IServiceCollection services)
    {
        services.AddSignalR();
        services.AddHostedService<SurfaceRelay>();

        return services;
    }

    /// <summary>
    /// The hub at <see cref="SurfaceHub.Path"/>. Over HTTPS it takes only a
    /// caller that presented a certificate — which the handshake has already
    /// checked against the host's anchors (<see cref="SurfaceBinding.UseXmipTls"/>)
    /// — and refuses any other with 403, told to <paramref name="refused"/>:
    /// a remote surface is an Xmip part, and Xmip's own traffic is mutual TLS
    /// (ADR-0063 clause 1). Over plain HTTP, which only loopback binds, it
    /// takes the caller as it is.
    /// </summary>
    public static WebApplication MapXmipSurfaceHub(
        this WebApplication app, Action<string>? refused = null)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseWhen(
            context => context.Request.IsHttps
                && context.Request.Path.StartsWithSegments(SurfaceHub.Path, StringComparison.Ordinal),
            guarded => guarded.Use(async (context, next) =>
            {
                if (await context.Connection.GetClientCertificateAsync(context.RequestAborted)
                    .ConfigureAwait(false) is not null)
                {
                    await next(context).ConfigureAwait(false);
                    return;
                }

                string why = $"REFUSED. {context.Connection.RemoteIpAddress} reached "
                    + $"{SurfaceHub.Path} over TLS without a certificate; a remote surface "
                    + "presents one (ADR-0063 clause 1)";
                refused?.Invoke(why);
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsync(why, context.RequestAborted)
                    .ConfigureAwait(false);
            }));
        app.MapHub<SurfaceHub>(SurfaceHub.Path);

        return app;
    }
}
