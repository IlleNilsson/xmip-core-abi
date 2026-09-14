namespace Xmip.Surface;

/// <summary>
/// What came of a <see cref="ScopeAction"/>: whether the runtime applied it,
/// and what it said. The executable and the PowerShell module emit this one
/// shape, so an exit code and a pipeline object agree.
/// </summary>
public sealed record ScopeOperation(
    string Scope, ScopeAction Action, bool Applied, string Result);
