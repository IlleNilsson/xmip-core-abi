namespace Xmip.Surface;

/// <summary>
/// The Playground's fake directory (ADR-0009, amendment 2026-09-14): it
/// allows the tester — the person who started the run — every role, and
/// knows no one else. A surface over a roll asks it like any other directory
/// and is told yes, so the role gate has one code path and no test-mode
/// bypass. A host has it only where its configuration names it
/// (<see cref="RoleAssignment.DirectoryKey"/> = <see cref="Kind"/>), which
/// <c>Start-XmipOperationWeb</c> does for a host following a roll.
/// </summary>
/// <param name="tester">The principal the directory allows.</param>
public sealed class TesterDirectory(string tester) : IDirectory
{
    /// <summary>The directory kind configuration names this one by.</summary>
    public const string Kind = "tester";

    /// <inheritdoc />
    public Role? RoleOf(string principal)
    {
        return string.Equals(principal, tester, StringComparison.OrdinalIgnoreCase)
            ? Role.Developer
            : null;
    }
}
