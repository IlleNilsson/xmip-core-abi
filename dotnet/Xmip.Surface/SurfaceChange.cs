namespace Xmip.Surface;

/// <summary>Which immutable operator snapshots may have advanced.</summary>
[Flags]
public enum SurfaceChangeKind
{
    None = 0,
    Health = 1,
    Measurements = 2,
    Topology = 4,
    Configuration = 8,
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
    public static SurfaceChange Initial(string source) =>
        new(0, SurfaceChangeKind.All, DateTimeOffset.UtcNow, source);
}
