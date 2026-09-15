using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;

namespace Xmip.Surface.Relay;

/// <summary>
/// Tells every connected <see cref="RemoteOperator"/> when the host's surface
/// changed: follows the host's one change feed and forwards each notice to
/// every client of <see cref="SurfaceHub"/>. Nothing here polls; the feed is
/// signalled, and so is the wire (ADR-0052, amendment 2026-09-15).
/// </summary>
public sealed class SurfaceRelay(IOperatorSurface surface, IHubContext<SurfaceHub> hub)
    : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (SurfaceChange change in surface.WatchAsync(stoppingToken)
            .ConfigureAwait(false))
        {
            await hub.Clients.All
                .SendAsync(RemoteOperator.ChangedMessage, change, stoppingToken)
                .ConfigureAwait(false);
        }
    }
}
