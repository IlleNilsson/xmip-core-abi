using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Xmip.Surface.Relay;

/// <summary>How a web host serves its surface: one call to register, one to map.</summary>
public static class SurfaceRelayExtensions
{
    /// <summary>
    /// SignalR and the relay that pushes the host's change feed. The hub
    /// grants each caller a role by the host's <see cref="RoleAssignment"/> —
    /// the one the host registered, else the one its configuration states
    /// (<see cref="RoleAssignment.From"/>) — and audits every act through the
    /// host's <see cref="ProgramAudit"/>, which the host registers (ADR-0062).
    /// </summary>
    public static IServiceCollection AddXmipSurfaceRelay(this IServiceCollection services)
    {
        services.TryAddSingleton(provider =>
            RoleAssignment.From(provider.GetRequiredService<IConfiguration>()));
        services.AddSignalR();
        services.AddHostedService<SurfaceRelay>();

        return services;
    }

    /// <summary>
    /// A browser's caller, per circuit (ADR-0009, amendment 2026-10-06): the
    /// <see cref="RoleContext"/> every screen of the circuit reads and acts
    /// by, the identity <see cref="UseXmipProvenCaller"/> proved for the
    /// request that opened it and the role the host's
    /// <see cref="RoleAssignment"/> grants it — never one role for the whole
    /// host.
    /// </summary>
    public static IServiceCollection AddXmipProvenCaller(this IServiceCollection services)
    {
        services.TryAddSingleton(provider =>
            RoleAssignment.From(provider.GetRequiredService<IConfiguration>()));
        services.AddScoped(provider => ProvenCaller.Of(
            provider.GetRequiredService<RoleAssignment>(),
            provider.GetRequiredService<AuthenticationStateProvider>()));

        return services;
    }

    /// <summary>
    /// Every request's user is the caller it proved, by
    /// <see cref="ProvenCaller.Prove"/>, before a page or a circuit reads it.
    /// </summary>
    public static WebApplication UseXmipProvenCaller(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.Use(async (context, next) =>
        {
            ProvenCaller.Prove(context);
            await next(context).ConfigureAwait(false);
        });

        return app;
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
