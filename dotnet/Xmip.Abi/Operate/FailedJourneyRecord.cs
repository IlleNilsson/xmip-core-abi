using System.Text.Json;

namespace Xmip.Abi.Operate;

/// <summary>
/// One Journey that failed, waiting in its Send Port's queue for an
/// operator's Retry or Dismiss (runtime-model.md section 13):
/// <c>observe::FailedJourney</c>, read from the JSON section 16 writes.
/// </summary>
/// <param name="Node">The node that sends its Send Port:
/// <c>xmip:///&lt;cluster&gt;/node/&lt;name&gt;</c>.</param>
/// <param name="SendPort">The Send Port it waits at, by name.</param>
/// <param name="Journey">The Journey's identifier: what an act names.</param>
/// <param name="Sequence">Its place in the queue, oldest lowest.</param>
/// <param name="Reason">Why it failed, in words.</param>
public sealed record FailedJourneyRecord(
    string Node, string SendPort, string Journey, ulong Sequence, string Reason);

/// <summary>
/// The Journeys that failed at one node's Send Port:
/// <c>observe::FailedJourneys</c>.
/// </summary>
/// <param name="Node">The node that sends the Port.</param>
/// <param name="SendPort">The Send Port, by name.</param>
/// <param name="Count">How many failed Journeys wait in its queue, as Xmip
/// Storage counts them.</param>
/// <param name="Next">The place in the Port's index of failed Journeys the
/// next page reads from, or null where the index was read to its end or the
/// list is a publication's, which carries only its node's window.</param>
/// <param name="Journeys">Those listed, oldest first.</param>
/// <param name="Blocked">Whether one of them blocks its sequence now: its
/// Send Port, or a Send Location it may leave by, names a correlation set
/// whose <c>on_failure</c> is <c>block</c>, so every Journey of its key waits
/// behind it until an operator retries or dismisses it.</param>
public sealed record FailedJourneyPort(
    string Node,
    string SendPort,
    ulong Count,
    ulong? Next,
    IReadOnlyList<FailedJourneyRecord> Journeys,
    bool Blocked = false);

/// <summary>
/// The Journeys that failed at the Send Ports a node or a publication
/// carries, and where an act is left when they are read through a
/// publication — empty where it is applied in the process itself.
/// </summary>
/// <param name="Orders">Where a surface leaves an act for the publisher.</param>
/// <param name="Ports">Each Send Port's, by node and Port.</param>
/// <param name="Listed">Whether the list is an answer: true where a node or
/// a publication was read, even with no Journey in it; false where the
/// surface could not list failed Journeys at all, or asked and was not
/// answered, so that its emptiness says nothing of the queue.</param>
/// <param name="Failure">Where the surface asked and was not answered — Xmip
/// Storage did not answer the node — what it was told, as one sentence that
/// opens <c>FAILED:</c>; empty otherwise, <see cref="Unlisted"/> among
/// them.</param>
public sealed record FailedJourneyList(
    string Orders,
    IReadOnlyList<FailedJourneyPort> Ports,
    bool Listed = true,
    string Failure = "")
{
    /// <summary>No answer: the surface cannot list failed Journeys — it
    /// reaches no node, no runtime is loaded, no publication was read.</summary>
    public static FailedJourneyList Unlisted { get; } = new(string.Empty, [], false);

    /// <summary>No answer, though the surface asked: <paramref name="why"/>,
    /// as what asked was told it.</summary>
    public static FailedJourneyList Unanswered(string why)
    {
        return new FailedJourneyList(string.Empty, [], false, $"FAILED: {why}");
    }

    /// <summary>Every Journey listed, Port by Port.</summary>
    public IEnumerable<FailedJourneyRecord> Journeys => Ports.SelectMany(port => port.Journeys);

    /// <summary>The list section 16 wrote.</summary>
    public static FailedJourneyList Parse(ReadOnlyMemory<byte> json)
    {
        using JsonDocument answer = JsonDocument.Parse(json);
        JsonElement root = answer.RootElement;

        return new FailedJourneyList(
            root.GetProperty("orders").GetString() ?? string.Empty,
            [.. root.GetProperty("failed_journeys").EnumerateArray().Select(Port)]);
    }

    private static FailedJourneyPort Port(JsonElement port)
    {
        string node = port.GetProperty("node").GetString() ?? string.Empty;
        string name = port.GetProperty("send_port").GetString() ?? string.Empty;
        JsonElement next = port.GetProperty("next");

        return new FailedJourneyPort(
            node,
            name,
            port.GetProperty("count").GetUInt64(),
            next.ValueKind == JsonValueKind.Number ? next.GetUInt64() : null,
            [
                .. port.GetProperty("journeys").EnumerateArray().Select(journey =>
                    new FailedJourneyRecord(
                        node,
                        name,
                        journey.GetProperty("journey").GetString() ?? string.Empty,
                        journey.GetProperty("sequence").GetUInt64(),
                        journey.GetProperty("reason").GetString() ?? string.Empty)),
            ],
            port.GetProperty("blocked").GetBoolean());
    }
}
