using Xmip.Abi.Operate;

namespace Xmip.Surface;

/// <summary>
/// One operator-facing item in the Xmip scope tree. CLI, PowerShell and GUI
/// consume this same shape; only their rendering differs.
/// </summary>
public sealed record ScopeItem(
    string Name,
    string Scope,
    bool IsContainer,
    HealthState? Health,
    byte? Severity,
    string Evidence,
    ulong? Received,
    ulong? Processed,
    ulong? Sent,
    ulong? Retrying,
    ulong? Failed,
    DateTimeOffset? Observed)
{
    /// <summary>Describe a scope from already-read health and activity.</summary>
    public static ScopeItem From(
        string scope, IReadOnlyList<HealthRecord> records, ActivitySummary activity)
    {
        int depth = ScopeTree.Parts(scope).Length;
        bool container = records.Any(record =>
            ScopeTree.Parts(record.Scope).Length > depth);
        HealthRecord? worst = ScopeTree.Worst(records);
        HealthState? health = container ? ScopeTree.Rollup(records) : worst?.State;

        return new ScopeItem(
            scope == ScopeTree.Root ? "cluster" : ScopeTree.Parts(scope).LastOrDefault() ?? scope,
            scope,
            container,
            health,
            worst?.Severity,
            worst?.Evidence ?? string.Empty,
            activity.Received,
            activity.Processed,
            activity.Sent,
            activity.Retrying,
            activity.Failed,
            activity.Observed ?? worst?.Observed);
    }

    /// <summary>Read a scope from the shared operator surface.</summary>
    public static ScopeItem Read(IOperatorSurface surface, string scope)
    {
        IReadOnlyList<HealthRecord> records = surface.Health(scope);
        return From(scope, records, surface.Activity(scope));
    }

    /// <summary>Read the direct children of a scope.</summary>
    public static IReadOnlyList<ScopeItem> Children(IOperatorSurface surface, string scope)
    {
        IReadOnlyList<HealthRecord> records = surface.Health(scope);

        return
        [
            .. ScopeTree.Branches(records, scope)
                .Select(branch => From(
                    branch.Scope,
                    [.. records.Where(record => ScopeTree.Beneath(record.Scope, branch.Scope))],
                    surface.Activity(branch.Scope))),
        ];
    }
}
