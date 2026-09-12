namespace Xmip.Surface;

/// <summary>An operation that can be applied uniformly to any Xmip scope.</summary>
public enum ScopeAction
{
    Pause,
    Resume,
    Start,
    Stop,
    Restart,
}

/// <summary>The structured result shared by CLI and PowerShell.</summary>
public sealed record ScopeOperation(
    string Scope, ScopeAction Action, bool Applied, string Result)
{
    /// <summary>Apply one action through the shared surface.</summary>
    public static ScopeOperation Apply(
        IOperatorSurface surface, string scope, ScopeAction action, string who)
    {
        string result = action switch
        {
            ScopeAction.Pause => surface.PauseScope(scope, who),
            ScopeAction.Resume => surface.ResumeScope(scope),
            ScopeAction.Start => surface.StartScope(scope, who),
            ScopeAction.Stop => surface.StopScope(scope, who),
            ScopeAction.Restart => surface.RestartScope(scope, who),
            _ => throw new ArgumentOutOfRangeException(nameof(action)),
        };

        bool applied = !result.Contains("unsupported", StringComparison.OrdinalIgnoreCase)
            && !result.StartsWith("nothing", StringComparison.OrdinalIgnoreCase)
            && !result.StartsWith("no runtime", StringComparison.OrdinalIgnoreCase)
            && !result.Contains("cannot be", StringComparison.OrdinalIgnoreCase);

        return new ScopeOperation(scope, action, applied, result);
    }
}
