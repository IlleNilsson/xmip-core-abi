using Xmip.Abi.Operate;

namespace Xmip.Surface;

/// <summary>
/// Who is at the keyboard and the role they hold (ADR-0009): the proven
/// caller and what <see cref="RoleAssignment"/> grants them. It is
/// <b>assigned, not chosen</b>: a person cannot promote themselves in the UI.
/// A screen shapes what it offers by it (ADR-0014 and ADR-0052, amendments
/// 2026-09-14): an Observer watches, an Operator also pauses, resumes and
/// configures, a Developer also opens configuration. Every act is decided by
/// it in <see cref="GatedOperator"/>, the one check every surface's acts pass,
/// by the <see cref="Caller"/> the runtime holds, never a name — so it lives
/// here, beneath both GUIs and the web host's caller proof, and not in
/// <c>Xmip.Gui</c>. A web host holds one per browser circuit, the desktop one
/// for its own user (ADR-0009, amendments 2026-10-06 and 2026-11-01).
/// </summary>
/// <param name="role">The role the caller holds.</param>
/// <param name="caller">The caller the runtime proved and holds for this
/// context, which it releases when disposed; null where nothing proved who
/// asks: such a caller watches and takes no act.</param>
public sealed class RoleContext(Role role, HeldCaller? caller) : IDisposable
{
    /// <summary>The assigned role. Read-only for the life of the caller.</summary>
    public Role Role { get; } = role;

    /// <summary>The caller the runtime holds, the one the gate takes; null
    /// where nothing proved who asks.</summary>
    public HeldCaller? Caller { get; } = caller;

    /// <summary>Who the caller is, as the runtime's gate answered, to show;
    /// null where nothing proved who asks. Never a caller itself.</summary>
    public string? Who => Caller?.Name;

    /// <summary>Whether this caller may reach configuration.</summary>
    public bool MayConfigure()
    {
        return Caller is not null && Role.MayConfigure();
    }

    /// <summary>Whether this caller may pause and resume the running estate.</summary>
    public bool MayOperate()
    {
        return Caller is not null && Role.MayOperate();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Caller?.Dispose();
    }
}
