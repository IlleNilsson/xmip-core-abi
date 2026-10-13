using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Xmip.Surface.Relay;

/// <summary>How a web host proves each browser's caller: one call to register, one to
/// use.</summary>
public static class SurfaceRelayExtensions
{
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
}
