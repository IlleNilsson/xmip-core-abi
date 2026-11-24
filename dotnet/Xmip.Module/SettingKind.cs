namespace Xmip.Module;

/// <summary>
/// What kind of value a unit's setting takes: the kinds a technology
/// declares a Location's settings in, as a Module's manifest writes them
/// (<c>abi::UnitSetting</c>).
/// </summary>
public enum SettingKind
{
    /// <summary>Text, <c>text</c>.</summary>
    Text,

    /// <summary>A whole number, <c>integer</c>, within its least and most.</summary>
    Integer,

    /// <summary><c>boolean</c>.</summary>
    Boolean,

    /// <summary>A time span written as <c>30s</c> or <c>250ms</c>,
    /// <c>duration</c>.</summary>
    Duration,

    /// <summary>Where something is reached, <c>address</c>.</summary>
    Address,

    /// <summary>A secret's name, <c>secret</c>.</summary>
    Secret,

    /// <summary>One of its words, <c>choice</c>.</summary>
    Choice,

    /// <summary>Text values by name, <c>table</c>.</summary>
    Table,
}
