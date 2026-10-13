namespace Xmip.Surface;

/// <summary>
/// What an operator stated for one invocation about which surface to read,
/// over what the host's document says: a node on another machine to follow,
/// a node on this one to follow, a published snapshot to read, and the
/// runtime library to load. The command line states it as <c>--remote</c>,
/// <c>--local</c>, <c>--snapshot</c> and <c>--runtime</c>; a cmdlet as
/// <c>-Remote</c>, <c>-Local</c>, <c>-Snapshot</c> and <c>-Library</c>; the
/// prompt as the snapshot <c>Start-XmipTest</c> told it to follow. Blank is
/// no statement. <see cref="SurfaceChoice.Stated"/> is the one precedence
/// over it.
/// </summary>
/// <param name="Remote">A node on another machine, at its operate address
/// (ADR-0014, amendment 2026-10-12).</param>
/// <param name="Snapshot">One published snapshot, one cluster (ADR-0052,
/// amendment 2026-09-20).</param>
/// <param name="Runtime">The runtime library, over the discovery rule: the
/// one the gRPC client and every call with no node behind it are made
/// through. It names no surface.</param>
/// <param name="Local">A node on this machine, at its local endpoint
/// (ADR-0014, amendment 2026-10-13).</param>
public sealed record SurfaceLine(
    string? Remote = null, string? Snapshot = null, string? Runtime = null,
    string? Local = null)
{
    /// <summary>Nothing stated: the document decides.</summary>
    public static SurfaceLine None { get; } = new();

    /// <summary>Why the line cannot be obeyed — a remote that names no node
    /// (<see cref="RemoteOperator.Refusal"/>) — or null when it can.</summary>
    public string? Refusal =>
        string.IsNullOrWhiteSpace(Remote) ? null : RemoteOperator.Refusal(Remote);
}
