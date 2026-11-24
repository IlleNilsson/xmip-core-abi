using System.Text.Json;
using Xmip.Module;

namespace XmipModule1;

/// <summary>
/// A Promote unit: an order's number, read from a JSON Message's member,
/// promoted into the Message's context as <c>OrderNumber</c> at receive,
/// where a Subscription's filter and every step after read it.
/// </summary>
public sealed class OrderNumber : PromoteUnit
{
    /// <summary>The context property the number is promoted into.</summary>
    public const string Property = "OrderNumber";

    /// <inheritdoc/>
    public override string Name => "OrderNumber";

    /// <inheritdoc/>
    public override IReadOnlyList<UnitSetting> Settings =>
    [
        new("member", SettingKind.Text, "The JSON member the order's number is read from.")
        {
            Default = "number",
        },
    ];

    /// <inheritdoc/>
    public override Promotion Open(StepSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        string member = settings.Text("member");

        return member.Length == 0
            ? throw new RefusedException("the step's member is empty")
            : new Reading(member);
    }

    /// <summary>The order's number in <paramref name="content"/>, a JSON
    /// object, under <paramref name="member"/>; none where it has no such
    /// member.</summary>
    /// <exception cref="RefusedException">The content is not JSON.</exception>
    public static string? Read(Stream content, string member)
    {
        try
        {
            using JsonDocument order = JsonDocument.Parse(content);

            return order.RootElement.ValueKind == JsonValueKind.Object
                && order.RootElement.TryGetProperty(member, out JsonElement number)
                && number.ValueKind == JsonValueKind.String
                    ? number.GetString()
                    : null;
        }
        catch (JsonException malformed)
        {
            throw new RefusedException("the content is not JSON", malformed);
        }
    }

    /// <summary>One step: the member it reads, the same for every
    /// Message, so any thread may call it.</summary>
    private sealed class Reading(string member) : Promotion
    {
        public override void Promote(Stream content, Promoted promoted)
        {
            if (Read(content, member) is { } number)
            {
                promoted.Text(Property, number);
            }
        }
    }
}
