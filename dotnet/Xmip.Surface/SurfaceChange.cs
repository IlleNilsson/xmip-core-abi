namespace Xmip.Surface;

/// <summary>Which immutable operator snapshots may have advanced.</summary>
[Flags]
public enum SurfaceChangeKind
{
    /// <summary>Nothing advanced.</summary>
    None = 0,

    /// <summary>The health records.</summary>
    Health = 1,

    /// <summary>The measurements.</summary>
    Measurements = 2,

    /// <summary>The communication topology.</summary>
    Topology = 4,

    /// <summary>The configuration.</summary>
    Configuration = 8,

    /// <summary>Every snapshot; what a watch announces first.</summary>
    All = Health | Measurements | Topology | Configuration,
}

/// <summary>
/// A wake-up notice, not operational data. Consumers reread the shared
/// surface after receiving it, so notices may be coalesced without losing the
/// current truth.
/// </summary>
public sealed record SurfaceChange(
    ulong Revision,
    SurfaceChangeKind Kind,
    DateTimeOffset Observed,
    string Source)
{
    /// <summary>Announce the view already available when a watch begins.</summary>
    public static SurfaceChange Initial(string source)
    {
        return new SurfaceChange(0, SurfaceChangeKind.All, DateTimeOffset.UtcNow, source);
    }
}
