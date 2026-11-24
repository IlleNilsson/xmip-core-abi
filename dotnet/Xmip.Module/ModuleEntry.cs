using System.Globalization;
using System.Text;

namespace Xmip.Module;

/// <summary>
/// The <c>[[modules]]</c> entry that declares a module's library in a
/// cluster's configuration, its manifest within it, as the runtime reads it
/// (<c>abi::ModuleManifest</c>, <c>abi::ModuleUnit</c>): written from the
/// exported unit's own declaration at build, beside the library, so the
/// unit is declared once, in its code (<c>build/Xmip.Module.targets</c>).
/// </summary>
public static class ModuleEntry
{
    /// <summary>The entry for the unit this assembly exports.</summary>
    /// <param name="module">The module's name: its entry's and its
    /// identity's, as a step entry's <c>module</c> names it.</param>
    /// <param name="version">The module's version, which a configuration
    /// pins.</param>
    /// <param name="library">Its library's name, without a platform's prefix
    /// or suffix: the node names the file as its own platform does.</param>
    /// <exception cref="InvalidOperationException">The assembly exports no
    /// unit.</exception>
    public static string OfExported(string module, string version, string library)
    {
        Unit unit = Export.Exported ?? throw new InvalidOperationException(
            "the assembly exports no unit: call Export.Unit from a [ModuleInitializer]");

        return Of(unit, module, version, library);
    }

    /// <summary>The entry for <paramref name="unit"/>; as
    /// <see cref="OfExported"/>.</summary>
    public static string Of(Unit unit, string module, string version, string library)
    {
        ArgumentNullException.ThrowIfNull(unit);

        StringBuilder entry = new();

        entry.Append("[[modules]]\n");
        Line(entry, "name", Quoted(module));
        entry.Append("start = true\n\n[modules.manifest]\noffline = true\n\n");
        entry.Append("[modules.manifest.identity]\n");
        Line(entry, "name", Quoted(module));
        Line(entry, "version", Quoted(version));
        entry.Append("\n[[modules.manifest.capabilities]]\n");
        Line(entry, "capability", Quoted(unit.Capability));
        entry.Append("execution_host = \"c-abi\"\ntrusted_required = false\n\n");
        entry.Append("[modules.manifest.entrypoint]\n");
        Line(entry, "library_path", Quoted(library));
        Line(entry, "symbol", Quoted(Abi.Module.ModuleAbi.Entrypoint));
        entry.Append("\n[[modules.manifest.units]]\n");
        Line(entry, "name", Quoted(unit.Name));
        Line(entry, "capability", Quoted(unit.Capability));
        Line(entry, "stages", Listed(unit.Stages));
        entry.Append("offline = true\n");

        foreach (UnitSetting setting in unit.Settings)
        {
            Setting(entry, setting);
        }

        return entry.ToString();
    }

    private static void Setting(StringBuilder entry, UnitSetting setting)
    {
        entry.Append("\n[[modules.manifest.units.settings]]\n");
        Line(entry, "name", Quoted(setting.Name));
        Line(entry, "kind", Quoted(Word(setting.Kind)));

        if (setting.Minimum is long minimum)
        {
            Line(entry, "minimum", minimum.ToString(CultureInfo.InvariantCulture));
        }

        if (setting.Maximum is long maximum)
        {
            Line(entry, "maximum", maximum.ToString(CultureInfo.InvariantCulture));
        }

        if (setting.Choices.Count > 0)
        {
            Line(entry, "choices", Listed(setting.Choices));
        }

        Line(entry, "presence", Quoted(setting.Presence));

        if (setting.Default is { } value)
        {
            Line(entry, "default", Written(value));
        }

        Line(entry, "meaning", Quoted(setting.Meaning));
    }

    private static void Line(StringBuilder entry, string key, string value)
    {
        entry.Append(key).Append(" = ").Append(value).Append('\n');
    }

    private static string Word(SettingKind kind)
    {
        return kind switch
        {
            SettingKind.Text => "text",
            SettingKind.Integer => "integer",
            SettingKind.Boolean => "boolean",
            SettingKind.Duration => "duration",
            SettingKind.Address => "address",
            SettingKind.Secret => "secret",
            SettingKind.Choice => "choice",
            SettingKind.Table => "table",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "no such kind"),
        };
    }

    private static string Written(object value)
    {
        return value switch
        {
            bool boolean => boolean ? "true" : "false",
            long integer => integer.ToString(CultureInfo.InvariantCulture),
            int integer => integer.ToString(CultureInfo.InvariantCulture),
            string text => Quoted(text),
            _ => throw new ArgumentException(
                $"a default is text, a whole number or a boolean, not {value.GetType().Name}",
                nameof(value)),
        };
    }

    private static string Listed(IEnumerable<string> words)
    {
        return "[" + string.Join(", ", words.Select(Quoted)) + "]";
    }

    /// <summary>A TOML basic string: quoted, its quote, backslash and control
    /// characters escaped.</summary>
    private static string Quoted(string text)
    {
        StringBuilder quoted = new("\"");

        foreach (char c in text)
        {
            _ = c switch
            {
                '"' => quoted.Append("\\\""),
                '\\' => quoted.Append("\\\\"),
                '\n' => quoted.Append("\\n"),
                '\t' => quoted.Append("\\t"),
                '\r' => quoted.Append("\\r"),
                _ when char.IsControl(c) =>
                    quoted.Append("\\u")
                        .Append(((int)c).ToString("X4", CultureInfo.InvariantCulture)),
                _ => quoted.Append(c),
            };
        }

        return quoted.Append('"').ToString();
    }
}
