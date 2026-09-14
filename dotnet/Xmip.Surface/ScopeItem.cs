using Xmip.Abi.Operate;

namespace Xmip.Surface;

/// <summary>
/// One row of the scope tree as every surface shows it: the name, the mood,
/// the worst leaf's severity and evidence (ADR-0052 clause 2), and the six
/// figures. The <c>xmip</c> executable, the PowerShell module and the GUI
/// read this one shape; only the rendering differs.
/// </summary>
public sealed record ScopeItem(
    string Name,
    string Scope,
    bool IsContainer,
    HealthState? Health,
    byte? Severity,
    string Evidence,
    Figures Figures,
    DateTimeOffset? Observed)
{
    /// <summary>Build a row from health already read and figures already read.</summary>
    public static ScopeItem From(
        string scope, IReadOnlyList<HealthRecord> records, Figures figures)
    {
        int depth = ScopeTree.Parts(scope).Length;
        bool container = records.Any(record => ScopeTree.Parts(record.Scope).Length > depth);
        HealthRecord? worst = ScopeTree.Worst(records);
        HealthState? health = container ? ScopeTree.Rollup(records) : worst?.State;

        return new ScopeItem(
            scope == ScopeTree.Root ? "cluster" : ScopeTree.Name(scope),
            scope,
            container,
            health,
            worst?.Severity,
            worst?.Evidence ?? string.Empty,
            figures,
            figures.Observed ?? worst?.Observed);
    }

    /// <summary>Read one row from a surface.</summary>
    public static ScopeItem Read(IOperatorSurface surface, string scope)
    {
        return From(scope, surface.Health(scope), surface.Figures(scope));
    }

    /// <summary>Read the direct children of a scope, one row each.</summary>
    public static IReadOnlyList<ScopeItem> Children(IOperatorSurface surface, string scope)
    {
        IReadOnlyList<HealthRecord> records = surface.Health(scope);

        return
        [
            .. ScopeTree.Branches(records, scope).Select(branch => From(
                branch.Scope,
                [.. records.Where(record => ScopeTree.Beneath(record.Scope, branch.Scope))],
                surface.Figures(branch.Scope))),
        ];
    }
}
