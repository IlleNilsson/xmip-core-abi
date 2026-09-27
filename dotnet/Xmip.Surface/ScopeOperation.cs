namespace Xmip.Surface;

/// <summary>
/// What came of a <see cref="ScopeAction"/>: whether the runtime applied it,
/// and what it said. The executable and the PowerShell module emit this one
/// shape, so an exit code and a pipeline object agree.
/// </summary>
public sealed record ScopeOperation(
    string Scope, ScopeAction Action, bool Applied, string Result)
{
    /// <summary>
    /// Who the runtime records as having acted — the evidence of the Paused
    /// mood: the name an operator stated (<c>xmip-cli pause --who</c>,
    /// <c>Suspend-XmipScope -Who</c>), else the user this process runs as.
    /// One rule for every surface; until 2026-09-27 the GUI said
    /// <c>operator</c> for everyone and only the cmdlet could be told a name.
    /// </summary>
    public static string Who(string? stated)
    {
        return string.IsNullOrWhiteSpace(stated) ? Environment.UserName : stated.Trim();
    }
}
