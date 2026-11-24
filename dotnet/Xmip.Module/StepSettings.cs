using System.Text.Json;

namespace Xmip.Module;

/// <summary>
/// What one step entry gives a unit: its settings as the node read them
/// through the unit's declaration, every default filled in, handed across
/// as one JSON object (<c>xmip_module.h</c> section 14), and the stage it is
/// called at.
/// </summary>
public sealed class StepSettings
{
    private readonly Dictionary<string, JsonElement> _given;

    private StepSettings(string stage, Dictionary<string, JsonElement> given)
    {
        Stage = stage;
        _given = given;
    }

    /// <summary>The stage word it is called at: <c>receive</c>,
    /// <c>process</c> or <c>send</c>.</summary>
    public string Stage { get; }

    /// <summary>The settings <paramref name="json"/> holds, at
    /// <paramref name="stage"/>.</summary>
    /// <exception cref="RefusedException">The JSON is not one object.</exception>
    public static StepSettings Read(string stage, string json)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(json.Length == 0 ? "{}" : json);

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new RefusedException("a step's settings are one JSON object");
            }

            Dictionary<string, JsonElement> given = [with(StringComparer.Ordinal)];

            foreach (JsonProperty property in document.RootElement.EnumerateObject())
            {
                given[property.Name] = property.Value.Clone();
            }

            return new StepSettings(stage, given);
        }
        catch (JsonException malformed)
        {
            throw new RefusedException("a step's settings are not JSON", malformed);
        }
    }

    /// <summary>Whether the step gives <paramref name="name"/>.</summary>
    public bool Has(string name)
    {
        return _given.ContainsKey(name);
    }

    /// <summary>A text, duration, address, secret or choice setting.</summary>
    /// <exception cref="RefusedException">The step does not give it as
    /// text.</exception>
    public string Text(string name)
    {
        JsonElement value = Given(name, JsonValueKind.String, "text");

        return value.GetString() ?? string.Empty;
    }

    /// <summary>An integer setting.</summary>
    /// <exception cref="RefusedException">The step does not give it as a
    /// whole number.</exception>
    public long Integer(string name)
    {
        JsonElement value = Given(name, JsonValueKind.Number, "a number");

        return value.TryGetInt64(out long integer)
            ? integer
            : throw new RefusedException($"the setting '{name}' is not a whole number");
    }

    /// <summary>A boolean setting.</summary>
    /// <exception cref="RefusedException">The step does not give it as a
    /// boolean.</exception>
    public bool Boolean(string name)
    {
        JsonElement value = Given(name);

        return value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : throw new RefusedException($"the setting '{name}' is not a boolean");
    }

    /// <summary>A table setting: text values by name, in the order
    /// given.</summary>
    /// <exception cref="RefusedException">The step does not give it as a
    /// table of text.</exception>
    public IReadOnlyList<KeyValuePair<string, string>> Table(string name)
    {
        JsonElement value = Given(name, JsonValueKind.Object, "a table");
        List<KeyValuePair<string, string>> table = [];

        foreach (JsonProperty entry in value.EnumerateObject())
        {
            if (entry.Value.ValueKind != JsonValueKind.String)
            {
                throw new RefusedException($"the setting '{name}' holds '{entry.Name}' as no text");
            }

            table.Add(new(entry.Name, entry.Value.GetString() ?? string.Empty));
        }

        return table;
    }

    private JsonElement Given(string name, JsonValueKind kind, string word)
    {
        JsonElement value = Given(name);

        return value.ValueKind == kind
            ? value
            : throw new RefusedException($"the setting '{name}' is not {word}");
    }

    private JsonElement Given(string name)
    {
        return _given.TryGetValue(name, out JsonElement value)
            ? value
            : throw new RefusedException($"the step gives no setting '{name}'");
    }
}
