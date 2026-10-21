namespace Xmip.Surface;

/// <summary>
/// The Playground's fake directory (ADR-0009, amendments 2026-09-14 and
/// 2026-10-21): it allows the tester — the person who started the run —
/// every role, and knows no one else. It knows the tester twice: as the
/// operating system user a host runs as, the desktop's caller, and by the
/// subject of the tester's operator certificate, <c>CN=&lt;person&gt;</c>,
/// the caller a browser proves to the web host (<see cref="TesterKey"/>). A
/// surface over a roll asks it like any other directory and is told yes, so
/// the role gate has one code path and no test-mode bypass. A host has it
/// only where its configuration names it
/// (<see cref="RoleAssignment.DirectoryKey"/> = <see cref="Kind"/>), which
/// <c>Start-XmipOperationWeb</c> does for a host following a roll.
/// </summary>
/// <param name="testers">The principals the directory allows; a blank one
/// allows no one.</param>
public sealed class TesterDirectory(IEnumerable<string?> testers) : IDirectory
{
    /// <summary>The directory kind configuration names this one by.</summary>
    public const string Kind = "tester";

    /// <summary>The configuration key, in the host's <c>[Xmip]</c> table,
    /// naming the subject of the tester's operator certificate:
    /// <c>CN=&lt;person&gt;</c>, as <c>New-XmipCertificate -Operator</c>
    /// names it.</summary>
    public const string TesterKey = "Xmip:Tester";

    private readonly string[] allowed =
    [
        .. testers.Where(tester => !string.IsNullOrWhiteSpace(tester))
            .Select(tester => tester!.Trim()),
    ];

    /// <inheritdoc />
    public Role? RoleOf(string principal)
    {
        return allowed.Any(tester =>
            string.Equals(principal, tester, StringComparison.OrdinalIgnoreCase))
            ? Role.Developer
            : null;
    }
}
