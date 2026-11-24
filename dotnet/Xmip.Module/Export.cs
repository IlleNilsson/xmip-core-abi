namespace Xmip.Module;

/// <summary>
/// The one unit a module's library exports. Called once, as the library
/// loads, from the module's own <c>[ModuleInitializer]</c>:
/// </summary>
/// <example>
/// <code>
/// internal static class Exported
/// {
///     [ModuleInitializer]
///     internal static void Unit() =&gt; Export.Unit(new OrderNumber());
/// }
/// </code>
/// </example>
/// <remarks>
/// A library holds one unit, its descriptor's standard naming it
/// (<c>xmip_module.h</c> sections 13 and 14): a second unit is a second
/// project.
/// </remarks>
public static class Export
{
    private static Unit? s_unit;

    /// <summary>The unit exported, where one is.</summary>
    internal static Unit? Exported => s_unit;

    /// <summary>Export <paramref name="unit"/> as this library's unit.</summary>
    /// <exception cref="InvalidOperationException">Another unit is exported
    /// already.</exception>
    public static void Unit(Unit unit)
    {
        ArgumentNullException.ThrowIfNull(unit);

        Unit? before = Interlocked.CompareExchange(ref s_unit, unit, null);

        if (before is not null && !ReferenceEquals(before, unit))
        {
            throw new InvalidOperationException(
                $"this library exports '{before.Name}' already, and a library holds one unit");
        }
    }
}
