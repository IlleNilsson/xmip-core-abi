using System.Text.Json;

namespace Xmip.Abi.Operate;

/// <summary>
/// One Event subscription as a node's hub holds it (ADR-0065, amendment
/// 2026-09-29): <c>observe::Subscription</c>, read from the JSON section 11
/// writes. <see cref="Node"/> and <see cref="Id"/> together name it.
/// </summary>
/// <param name="Node">The node whose hub holds it:
/// <c>xmip:///&lt;cluster&gt;/node/&lt;name&gt;</c>.</param>
/// <param name="Id">Its number in that hub.</param>
/// <param name="Subscriber">The Party subscribed, by the name it was declared
/// with — what an operator reads; empty where it was declared with none.</param>
/// <param name="Party">The Party subscribed, by its UUID.</param>
/// <param name="Action">What its filter asks for, in words.</param>
/// <param name="Scope">What its filter reaches.</param>
/// <param name="State">Its state as the runtime words it: active or paused.</param>
/// <param name="Paused">Whether its delivery is held.</param>
/// <param name="Queued">Events waiting in its queue.</param>
/// <param name="Capacity">How many its queue holds.</param>
/// <param name="Delivered">Events handed over since it was made.</param>
/// <param name="Missed">Matching Events a full queue refused since it was made.</param>
/// <param name="Since">When it was made.</param>
public sealed record SubscriptionRecord(
    string Node,
    ulong Id,
    string Subscriber,
    string Party,
    string Action,
    string Scope,
    string State,
    bool Paused,
    ulong Queued,
    ulong Capacity,
    ulong Delivered,
    ulong Missed,
    DateTimeOffset Since);

/// <summary>
/// The subscriptions a hub or a publication carries, and where acts on them
/// are left when they are read through a publication — empty where they are
/// applied in the process itself.
/// </summary>
/// <param name="Orders">Where a surface leaves an act for the publisher.</param>
/// <param name="Subscriptions">The subscriptions, by node and number.</param>
public sealed record SubscriptionList(string Orders, IReadOnlyList<SubscriptionRecord> Subscriptions)
{
    /// <summary>No subscription, nowhere to leave an act.</summary>
    public static SubscriptionList Empty { get; } = new(string.Empty, []);

    /// <summary>The list section 11 wrote.</summary>
    public static SubscriptionList Parse(ReadOnlyMemory<byte> json)
    {
        using JsonDocument answer = JsonDocument.Parse(json);
        JsonElement root = answer.RootElement;

        return new SubscriptionList(
            root.GetProperty("orders").GetString() ?? string.Empty,
            [.. root.GetProperty("subscriptions").EnumerateArray().Select(Record)]);
    }

    private static SubscriptionRecord Record(JsonElement entry)
    {
        return new SubscriptionRecord(
            entry.GetProperty("node").GetString() ?? string.Empty,
            entry.GetProperty("id").GetUInt64(),
            entry.GetProperty("subscriber").GetString() ?? string.Empty,
            entry.GetProperty("party").GetString() ?? string.Empty,
            entry.GetProperty("action").GetString() ?? string.Empty,
            entry.GetProperty("scope").GetString() ?? string.Empty,
            entry.GetProperty("state").GetString() ?? string.Empty,
            entry.GetProperty("paused").GetBoolean(),
            entry.GetProperty("queued").GetUInt64(),
            entry.GetProperty("capacity").GetUInt64(),
            entry.GetProperty("delivered").GetUInt64(),
            entry.GetProperty("missed").GetUInt64(),
            Operator.FromNanos(entry.GetProperty("since_unix_nanos").GetInt64()));
    }
}
