namespace Xmip.Module;

/// <summary>
/// A Promote unit: values read out of a Message's content into its context,
/// at receive alone (<c>xmip_module.h</c> section 14).
/// </summary>
/// <example>
/// <code>
/// public sealed class OrderNumber : PromoteUnit
/// {
///     public override string Name => "OrderNumber";
///
///     public override Promotion Open(StepSettings settings) =&gt;
///         new Reading(settings.Text("member"));
/// }
/// </code>
/// </example>
public abstract class PromoteUnit : Unit
{
    /// <inheritdoc/>
    internal sealed override string Capability => "promote";

    /// <inheritdoc/>
    internal sealed override IReadOnlyList<string> Stages => ["receive"];

    /// <summary>
    /// Open the unit for one step entry: once, when the node opens the step,
    /// never per Message.
    /// </summary>
    /// <param name="settings">What the step entry gives, every default its
    /// declaration has filled in.</param>
    /// <returns>The step opened, which promotes every Message the entry
    /// is called for.</returns>
    /// <exception cref="RefusedException">The settings are not what the unit
    /// can work with; the message says why.</exception>
    public abstract Promotion Open(StepSettings settings);
}
