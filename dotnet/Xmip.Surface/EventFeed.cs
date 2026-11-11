using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Xmip.Abi.Operate;

namespace Xmip.Surface;

/// <summary>
/// A .NET program's subscription to the Events of its own process
/// (ADR-0065): through the runtime's library that
/// <see cref="RuntimeLibrary"/>'s one rule found, as the Party the account
/// the program runs as proves to be among the Parties of the node serving in
/// its process — never one it names (ADR-0019, amendment 2026-11-11) —
/// audited where the program's own records go (<paramref name="audit"/>,
/// ADR-0062). Which Events match, who may see them, the queue and the audit
/// of every subscription, delivery and refusal are <c>xmip-core-event</c>'s;
/// this only opens and reads.
/// </summary>
/// <remarks>
/// Only code in a node's own process can subscribe, and no export lets a
/// node serve in a face's process (<c>xmip_start_v1</c> runs nothing), so in
/// a face every subscription is refused today and no face calls this; its
/// draining and following are untested until a node serves in the process
/// (open problem 66). Events of a node in another process reach a subscriber
/// over the wire (ADR-0065 clause 3), not through this.
/// </remarks>
/// <param name="audit">The program subscribing, and where its records
/// go.</param>
public sealed class EventFeed(ProgramAudit audit)
{
    // How many Events a follower may hold before the runtime's own queue
    // holds the rest, and counts what it refuses.
    private const int Held = 1024;

    /// <summary>The program subscribing.</summary>
    public ProgramAudit Audit { get; } = audit ?? throw new ArgumentNullException(nameof(audit));

    /// <summary>
    /// A subscription to drain (<see cref="EventSubscription.Next"/>), which
    /// the caller disposes. <paramref name="capacity"/> bounds its queue, 0
    /// for the runtime's default.
    /// </summary>
    /// <exception cref="UnauthorizedAccessException">No node serving in this
    /// process holds its account as a Party, or the authorization gate refused
    /// the Party; the message is the sentence.</exception>
    /// <exception cref="InvalidOperationException">No runtime library could be
    /// loaded.</exception>
    public EventSubscription Subscribe(EventFilter? filter = null, int capacity = 0)
    {
        return RuntimeLibrary.Rules.Events.Subscribe(
            Audit.Program, Audit.Directory, filter, capacity);
    }

    /// <summary>
    /// Every Event <paramref name="filter"/> asks for, as it arrives, until
    /// <paramref name="cancel"/>: the runtime calls back the moment one is
    /// published, and the subscription ends with the enumeration.
    /// </summary>
    /// <exception cref="UnauthorizedAccessException">As
    /// <see cref="Subscribe"/>.</exception>
    public async IAsyncEnumerable<EventRecord> Follow(
        EventFilter? filter = null,
        [EnumeratorCancellation] CancellationToken cancel = default)
    {
        Channel<EventRecord> arrived = Channel.CreateBounded<EventRecord>(
            new BoundedChannelOptions(Held) { SingleReader = true, SingleWriter = true });

        // The runtime's listener thread is the channel's writer, and waits
        // there while the follower is full; completing the channel first is
        // what lets unsubscribing wait for that callback to return.
        EventSubscription subscription = RuntimeLibrary.Rules.Events.Listen(
            Audit.Program, Audit.Directory, heard => Hand(arrived.Writer, heard), filter);

        try
        {
            while (await arrived.Reader.WaitToReadAsync(cancel).ConfigureAwait(false))
            {
                while (arrived.Reader.TryRead(out EventRecord? heard))
                {
                    yield return heard;
                }
            }
        }
        finally
        {
            arrived.Writer.TryComplete();
            subscription.Dispose();
        }
    }

    /// <summary>Hand <paramref name="record"/> to every matching subscription
    /// in this process; how many took it (<see cref="RuntimeEvents.Publish"/>).</summary>
    public static int Publish(EventRecord record)
    {
        return RuntimeLibrary.Rules.Events.Publish(record);
    }

    // On the runtime's listener thread, which may wait: false from the wait
    // is a follower that has gone, and the Event is its no more.
    private static void Hand(ChannelWriter<EventRecord> writer, EventRecord heard)
    {
        while (!writer.TryWrite(heard))
        {
            if (!writer.WaitToWriteAsync().AsTask().GetAwaiter().GetResult())
            {
                return;
            }
        }
    }
}
