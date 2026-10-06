namespace Xmip.Surface;

/// <summary>
/// Who is at the keyboard and the role they hold (ADR-0009): the proven
/// caller and what <see cref="RoleAssignment"/> grants them. It is
/// <b>assigned, not chosen</b>: a person cannot promote themselves in the UI.
/// A screen shapes what it offers by it (ADR-0014 and ADR-0052, amendments
/// 2026-09-14): an Observer watches, an Operator also pauses, resumes and
/// configures, a Developer also opens configuration. Every act is decided by
/// it in <see cref="GatedOperator"/>, the one check every surface's acts pass,
/// and taken as <see cref="Who"/> — so it lives here, beneath both GUIs and
/// the relay, and not in <c>Xmip.Gui</c>. A web host holds one per browser
/// circuit, the desktop one for its own user (ADR-0009, amendment 2026-10-06).
/// </summary>
/// <param name="role">The role the caller holds.</param>
/// <param name="who">The identity the caller proved, or null where nothing
/// proved it: such a caller watches and takes no act.</param>
public sealed class RoleContext(Role role, string? who)
{
    /// <summary>The assigned role. Read-only for the life of the caller.</summary>
    public Role Role { get; } = role;

    /// <summary>The proven caller, or null where nothing proved who asks.</summary>
    public string? Who { get; } = who;

    /// <summary>Whether this caller may reach configuration.</summary>
    public bool MayConfigure()
    {
        return Who is not null && Role.MayConfigure();
    }

    /// <summary>Whether this caller may pause and resume the running estate.</summary>
    public bool MayOperate()
    {
        return Who is not null && Role.MayOperate();
    }
}
