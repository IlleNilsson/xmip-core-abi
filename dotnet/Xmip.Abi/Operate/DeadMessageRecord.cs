using System.Text.Json;

namespace Xmip.Abi.Operate;

/// <summary>
/// One Message in one node's Dead Message Queue (ADR-0052, amendment
/// 2026-10-01): <c>observe::DeadMessage</c>, read from the JSON section 15
/// writes. <see cref="Node"/> and <see cref="Message"/> together name it. An
/// accepted Message that no Subscription matched is kept in the Ledger with
/// its receive context, what its gates concluded, its promoted properties
/// and every Subscription's reason for declining; an Operator replays it
/// once a Subscription is added or fixed. It is not a dead letter queue: a
/// failed Journey never goes there.
/// </summary>
/// <param name="Node">The node whose queue keeps it:
/// <c>xmip:///&lt;cluster&gt;/node/&lt;name&gt;</c>.</param>
/// <param name="Message">The Message's identifier: what a Replay names.</param>
/// <param name="Sequence">Its place in the queue, oldest lowest.</param>
/// <param name="ReceiveLocation">The Receive Location it arrived at: the
/// header's <c>location</c>, named in full so it is never read as where a
/// drill stands.</param>
/// <param name="Received">When it was received.</param>
/// <param name="Validation">Each gate and its verdict, in the order they
/// ran.</param>
/// <param name="Promoted">Each promoted property, name and value, by
/// name.</param>
/// <param name="Declines">Each Subscription asked and why it declined, in
/// the order asked.</param>
public sealed record DeadMessageRecord(
    string Node,
    string Message,
    ulong Sequence,
    string ReceiveLocation,
    DateTimeOffset Received,
    IReadOnlyList<KeyValuePair<string, string>> Validation,
    IReadOnlyList<KeyValuePair<string, string>> Promoted,
    IReadOnlyList<KeyValuePair<string, string>> Declines);

/// <summary>
/// The Dead Message Queue entries a node or a publication carries, and where
/// a Replay is left when they are read through a publication — empty where
/// it is applied in the process itself.
/// </summary>
/// <param name="Orders">Where a surface leaves an act for the publisher.</param>
/// <param name="DeadMessages">The entries, by node and Message.</param>
public sealed record DeadMessageList(
    string Orders, IReadOnlyList<DeadMessageRecord> DeadMessages)
{
    /// <summary>No entry, nowhere to leave an act.</summary>
    public static DeadMessageList Empty { get; } = new(string.Empty, []);

    /// <summary>The list section 15 wrote.</summary>
    public static DeadMessageList Parse(ReadOnlyMemory<byte> json)
    {
        using JsonDocument answer = JsonDocument.Parse(json);
        JsonElement root = answer.RootElement;

        return new DeadMessageList(
            root.GetProperty("orders").GetString() ?? string.Empty,
            [.. root.GetProperty("dead_messages").EnumerateArray().Select(Record)]);
    }

    private static DeadMessageRecord Record(JsonElement entry)
    {
        string Text(string name)
        {
            return entry.GetProperty(name).GetString() ?? string.Empty;
        }

        return new DeadMessageRecord(
            Text("node"),
            Text("message"),
            entry.GetProperty("sequence").GetUInt64(),
            Text("location"),
            Operator.FromNanos(entry.GetProperty("received_unix_nanos").GetInt64()),
            Pairs(entry.GetProperty("validation")),
            Pairs(entry.GetProperty("promoted")),
            Pairs(entry.GetProperty("declines")));
    }

    // An array of [name, value] pairs, in the order written.
    private static KeyValuePair<string, string>[] Pairs(JsonElement pairs)
    {
        return
        [
            .. pairs.EnumerateArray().Select(pair => new KeyValuePair<string, string>(
                pair[0].GetString() ?? string.Empty, pair[1].GetString() ?? string.Empty)),
        ];
    }
}
