using Xmip.Abi.Operate;

namespace Xmip.Surface;

/// <summary>
/// One row of the scope tree as every surface shows it: the name, the mood,
/// the worst leaf beneath it — its scope, severity and evidence, so a Holding
/// row says why and where (ADR-0052 clause 2) — and the six figures. The
/// <c>xmip</c> executable, the PowerShell module and the GUI read this one
/// shape; only the rendering differs.
/// </summary>
/// <param name="Name">What the row is called within its cluster
/// (<see cref="ScopeTree.Name"/>).</param>
/// <param name="Scope">The row's own scope.</param>
/// <param name="IsContainer">Whether anything is beneath it.</param>
/// <param name="Health">Its mood: a leaf's own, a container's rollup.</param>
/// <param name="Severity">The worst leaf's severity.</param>
/// <param name="Evidence">The worst leaf's evidence.</param>
/// <param name="Worst">The worst leaf's scope — the next place to drill to
/// the cause, the row itself for a leaf; null where nothing is recorded.
/// Until 2026-09-26 a row said the evidence and never whose it was, so an
/// operator read why and could not reach it.</param>
/// <param name="Figures">The six figures at the row, summed beneath it.</param>
/// <param name="Observed">When the newest of them was observed.</param>
public sealed record ScopeItem(
    string Name,
    string Scope,
    bool IsContainer,
    HealthState? Health,
    byte? Severity,
    string Evidence,
    string? Worst,
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
            worst?.Scope,
            figures,
            figures.Observed ?? worst?.Observed);
    }

    /// <summary>Read one row from a surface.</summary>
    public static ScopeItem Read(IOperatorSurface surface, string scope)
    {
        return From(scope, surface.Health(scope), surface.Figures(scope));
    }

    /// <summary>Read the direct children of a scope, one row each, worst
    /// first.</summary>
    public static IReadOnlyList<ScopeItem> Children(IOperatorSurface surface, string scope)
    {
        ScopeIndex index = surface.Index();

        return
        [
            .. index.Branches(scope).Select(branch => From(
                branch.Scope, index.Health(branch.Scope), surface.Figures(branch.Scope))),
        ];
    }

    /// <summary>
    /// The rows a selection names, as every surface answers <c>show</c>:
    /// one per scope that <see cref="Exists"/>, and — where a wildcard named
    /// several — worst first, each standing as its worst leaf's mood and
    /// severity under its own scope, as a level of the tree reads. A literal
    /// scope is its one row, or none. Until 2026-09-27 the cmdlet filtered and
    /// ordered its rows and the command line did neither.
    /// </summary>
    public static IReadOnlyList<ScopeItem> Selected(
        IOperatorSurface surface, ScopeSelection chosen)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(chosen);

        ScopeItem[] rows = [.. chosen.Scopes.Select(surface.Describe).Where(row => row.Exists)];

        if (!chosen.Patterned || rows.Length < 2)
        {
            return rows;
        }

        ScopeIndex index = surface.Index();

        return ScopeTree.WorstFirst(rows, row => Standing(index, row));
    }

    /// <summary>Whether the row needs the operator: its mood is not Fine.</summary>
    public bool Troubled => Health is { } mood && mood != HealthState.Fine;

    /// <summary>Whether the scope is there at all: something recorded health
    /// at or beneath it, or it has a figure. A scope with neither is nothing
    /// the surface holds, and every surface says so the same way
    /// (<see cref="English.NothingAt"/>).</summary>
    public bool Exists => Health is not null || Figures.HasValues;

    // A row as it stands in the worst-first order: its worst leaf's mood and
    // severity under its own scope, as a branch of the tree stands.
    private static HealthRecord Standing(ScopeIndex index, ScopeItem row)
    {
        HealthRecord? worst = index.Worst(row.Scope);

        return new HealthRecord(
            row.Scope,
            worst?.State ?? HealthState.Fine,
            worst?.Severity ?? 0,
            string.Empty,
            default);
    }
}
