using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
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

    /// <summary>The hub at <see cref="SurfaceHub.Path"/>.</summary>
    public static IEndpointRouteBuilder MapXmipSurfaceHub(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHub<SurfaceHub>(SurfaceHub.Path);

        return endpoints;
    }
}
