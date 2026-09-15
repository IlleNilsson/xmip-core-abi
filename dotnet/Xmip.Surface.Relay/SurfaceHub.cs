using Microsoft.AspNetCore.SignalR;
using Xmip.Abi.Operate;

namespace Xmip.Surface.Relay;

/// <summary>
/// The host's surface, served: each method answers what the host's own
/// <see cref="IOperatorSurface"/> answers, so a <see cref="RemoteOperator"/>
/// on another machine reads the same records a screen on this one reads.
/// Changes are not asked for here; <see cref="SurfaceRelay"/> pushes them.
/// </summary>
/// <remarks>
/// The two acts cross the wire as they cross the desktop. Who asked is what
/// the caller said, until the role gate ADR-0009 queues makes it what the
/// directory proved.
/// </remarks>
public sealed class SurfaceHub(IOperatorSurface surface) : Hub
{
    /// <summary>Where a web host maps this hub.</summary>
    public const string Path = RemoteOperator.HubPath;

    /// <summary>Where the host's records come from.</summary>
    public string Source()
    {
        return surface.Source;
    }

    /// <summary>Health at and beneath a scope, worst first.</summary>
    public IReadOnlyList<HealthRecord> Health(string scope)
    {
        return surface.Health(scope);
    }

    /// <summary>One kind of count, summed over the scope.</summary>
    public MeasurementRecord? Measure(string scope, Counted counted)
    {
        return surface.Measure(scope, counted);
    }

    /// <summary>The configured and observed communication topology.</summary>
    public TopologySnapshot Topology()
    {
        return surface.Topology();
    }

    /// <summary>Pause everything at and beneath a scope.</summary>
    public string Pause(string scope, string who)
    {
        return surface.PauseScope(scope, who);
    }

    /// <summary>Resume everything at and beneath a scope.</summary>
    public string Resume(string scope)
    {
        return surface.ResumeScope(scope);
    }
}
