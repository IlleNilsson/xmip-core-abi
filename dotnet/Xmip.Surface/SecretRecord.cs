using System.Text.Json;

namespace Xmip.Surface;

/// <summary>
/// A secret a node's Xmip Storage keeps, as a surface lists it: its name,
/// its kind — password, key or certificate — and when and by whom it was
/// last set (ADR-0019, amendment 2026-10-29). Never its value: no surface
/// reads a secret's value back out.
/// </summary>
public sealed record SecretRecord(string Name, string Kind, DateTimeOffset Set, string SetBy)
{
    /// <summary>The kinds a secret is, in the runtime's words
    /// (<c>xcore::SecretKind</c>).</summary>
    public static IReadOnlyList<string> Kinds { get; } = ["password", "key", "certificate"];

    /// <summary>The secrets a listing the runtime wrote holds:
    /// <c>{"secrets":[{"name","kind","set_unix_nanos","set_by"}]}</c>.</summary>
    public static IReadOnlyList<SecretRecord> Listed(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        using JsonDocument document = JsonDocument.Parse(json);

        if (!document.RootElement.TryGetProperty("secrets", out JsonElement secrets))
        {
            return [];
        }

        return
        [
            .. secrets.EnumerateArray().Select(secret => new SecretRecord(
                secret.GetProperty("name").GetString() ?? string.Empty,
                secret.GetProperty("kind").GetString() ?? string.Empty,
                DateTimeOffset.UnixEpoch.AddTicks(
                    (long)(secret.GetProperty("set_unix_nanos").GetUInt64() / 100)),
                secret.GetProperty("set_by").GetString() ?? string.Empty)),
        ];
    }
}

/// <summary>
/// What came of setting the secret <paramref name="Name"/> through the node
/// at <paramref name="Node"/>: whether it was set, and what was said — never
/// its value.
/// </summary>
public sealed record SecretOperation(string Node, string Name, bool Applied, string Result)
{
    /// <summary>The act's word, as the node's audit records it.</summary>
    public const string Act = "set";

    /// <summary>Not set, with why.</summary>
    public static SecretOperation Declined(string node, string name, string why)
    {
        return new SecretOperation(node, name, false, why);
    }
}
