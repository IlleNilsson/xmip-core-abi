namespace Xmip.Module;

/// <summary>
/// A compiled unit: a function a Module provides, which a configuration's
/// step entry calls with its settings (<c>doc/terminology.md</c>, Compiled
/// unit; ADR-0066, amendment 2026-10-22). A library holds one, exported with
/// <see cref="Export.Unit"/>.
/// </summary>
/// <remarks>
/// Derived through the capability's own class, <see cref="PromoteUnit"/>,
/// which says what the unit can be called as and at which stages; the
/// capabilities whose tables <c>xmip_module.h</c> does not declare yet have
/// no class here (ADR-0075).
/// </remarks>
public abstract class Unit
{
    /// <summary>Its name, as a step entry's <c>unit</c> names it. Never read
    /// for meaning: what the unit is called as is its capability.</summary>
    public abstract string Name { get; }

    /// <summary>What a step entry may give it, in declared order, written
    /// into its Module's manifest.</summary>
    public virtual IReadOnlyList<UnitSetting> Settings => [];

    /// <summary>What it can be called as, by the manifest's word.</summary>
    internal abstract string Capability { get; }

    /// <summary>The stages it serves, by the message path's words.</summary>
    internal abstract IReadOnlyList<string> Stages { get; }
}
