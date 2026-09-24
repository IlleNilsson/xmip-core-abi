namespace Xmip.Surface;

/// <summary>
/// What an operator stated for one invocation about which surface to read,
/// over what the host's document says: a web host to follow, a published
/// snapshot to read, or a runtime library to load. The command line states
/// it as <c>--remote</c>, <c>--snapshot</c> and <c>--runtime</c>; a cmdlet as
/// <c>-Remote</c>, <c>-Snapshot</c> and <c>-Library</c>; the prompt as the
/// snapshot <c>Start-XmipTest</c> told it to follow. Blank is no statement.
/// <see cref="SurfaceChoice.Stated"/> is the one precedence over it.
/// </summary>
/// <param name="Remote">A web host on another machine (ADR-0052, amendment
/// 2026-09-15).</param>
/// <param name="Snapshot">One published snapshot, one cluster (ADR-0052,
/// amendment 2026-09-20).</param>
/// <param name="Runtime">A runtime library, over the discovery rule.</param>
public sealed record SurfaceLine(
    string? Remote = null, string? Snapshot = null, string? Runtime = null)
{
    /// <summary>Nothing stated: the document decides.</summary>
    public static SurfaceLine None { get; } = new();
}
