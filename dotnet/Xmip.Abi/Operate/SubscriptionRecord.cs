using System.Text.Json;

namespace Xmip.Abi.Operate;

/// <summary>
/// One Subscription as a node routes by it (ADR-0013, amendment
/// 2026-09-30): <c>observe::Subscription</c>, read from the JSON section 14
/// writes. <see cref="Node"/> and <see cref="Name"/> together name it. A
/// Subscription picks a published Message up and opens a Journey; it is
/// configuration, added and removed in the TOML of the Xmip Application that
/// draws it, and an operator pauses and resumes it — never removes it.
/// </summary>
/// <param name="Node">The node that routes by it:
/// <c>xmip:///&lt;cluster&gt;/node/&lt;name&gt;</c>.</param>
/// <param name="Name">Its configured name, unique on its node.</param>
/// <param name="Application">The Xmip Application that draws it.</param>
/// <param name="Filter">What it subscribes to: its filter, as configured.</param>
/// <param name="Destination">Where it leads, in words.</param>
/// <param name="File">The file its Application was read from.</param>
/// <param name="Configuration">Its entry in that file, as the file says it.</param>
/// <param name="State">Its state as the runtime words it: active or paused.</param>
/// <param name="Paused">Whether what it matches is held.</param>
/// <param name="By">Who paused it; empty while it is active.</param>
/// <param name="PickedUp">Messages it picked up since its node started.</param>
/// <param name="Held">Messages it holds, matched while paused and not yet
/// picked up.</param>
/// <param name="Since">When its state began.</param>
public sealed record SubscriptionRecord(
    string Node,
    string Name,
    string Application,
    string Filter,
    string Destination,
    string File,
    string Configuration,
    string State,
    bool Paused,
    string By,
    ulong PickedUp,
    ulong Held,
    DateTimeOffset Since);

/// <summary>
/// The Subscriptions a node or a publication carries, and where acts on them
/// are left when they are read through a publication — empty where they are
/// applied in the process itself.
/// </summary>
/// <param name="Orders">Where a surface leaves an act for the publisher.</param>
/// <param name="Subscriptions">The Subscriptions, by node and name.</param>
public sealed record SubscriptionList(
    string Orders, IReadOnlyList<SubscriptionRecord> Subscriptions)
{
    /// <summary>No Subscription, nowhere to leave an act.</summary>
    public static SubscriptionList Empty { get; } = new(string.Empty, []);

    /// <summary>The list section 14 wrote.</summary>
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
        string Text(string name)
        {
            return entry.GetProperty(name).GetString() ?? string.Empty;
        }

        return new SubscriptionRecord(
            Text("node"),
            Text("name"),
            Text("application"),
            Text("filter"),
            Text("destination"),
            Text("file"),
            Text("configuration"),
            Text("state"),
            entry.GetProperty("paused").GetBoolean(),
            Text("by"),
            entry.GetProperty("picked_up").GetUInt64(),
            entry.GetProperty("held").GetUInt64(),
            Operator.FromNanos(entry.GetProperty("since_unix_nanos").GetInt64()));
    }
}
