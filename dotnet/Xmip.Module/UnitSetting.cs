namespace Xmip.Module;

/// <summary>
/// One setting a step entry may give a unit, declared once beside the unit
/// and written into its Module's manifest, where the node reads a step's
/// settings through it and fills in its default before the unit opens.
/// </summary>
/// <param name="Name">Its name, as a step entry's <c>settings</c> table
/// writes it.</param>
/// <param name="Kind">What kind of value it takes.</param>
/// <param name="Meaning">What it means, in one sentence.</param>
public sealed record UnitSetting(string Name, SettingKind Kind, string Meaning)
{
    /// <summary>The value a step that gives none has: text for text, an
    /// address, a secret, a choice and a duration; a <see cref="long"/> for an
    /// integer; a <see cref="bool"/> for a boolean. None makes the setting
    /// required, unless <see cref="Optional"/>.</summary>
    public object? Default { get; init; }

    /// <summary>A step may leave it out, and the unit then has no value.</summary>
    public bool Optional { get; init; }

    /// <summary>A choice's words.</summary>
    public IReadOnlyList<string> Choices { get; init; } = [];

    /// <summary>An integer's least.</summary>
    public long? Minimum { get; init; }

    /// <summary>An integer's most.</summary>
    public long? Maximum { get; init; }

    /// <summary>How present it must be, by the manifest's word:
    /// <c>default</c>, <c>optional</c> or <c>required</c>.</summary>
    internal string Presence =>
        Default is not null ? "default" : Optional ? "optional" : "required";
}
