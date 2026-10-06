namespace Xmip.Surface;

/// <summary>
/// What a directory answers (ADR-0009, amendment 2026-09-14): which of
/// Observer, Operator and Developer it grants a proven principal. Xmip keeps
/// no user store and assigns no role of its own; it asks. Handed to
/// <see cref="RoleAssignment"/> by whoever composes the host (ADR-0068).
/// </summary>
public interface IDirectory
{
    /// <summary>The role the directory grants <paramref name="principal"/>,
    /// or null where it grants none.</summary>
    public Role? RoleOf(string principal);
}
