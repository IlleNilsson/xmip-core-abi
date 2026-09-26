using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Xmip.Abi.Operate;

namespace Xmip.Surface;

/// <summary>
/// Events as every .NET face subscribes to them (ADR-0065): through the
/// runtime's library that <see cref="RuntimeLibrary"/>'s one rule found, as
/// the Party <paramref name="subscriber"/>, audited where the program's own
/// records go (<paramref name="audit"/>, ADR-0062). Which Events match, who
/// may see them, the queue and the audit of every subscription, delivery and
/// refusal are <c>xmip-core-event</c>'s; this only opens and reads.
/// </summary>
/// <remarks>
/// In process: what a face hears is what is published in the process that
/// loaded the library — a node it started, or <see cref="Publish"/>. Events
/// of a node in another process reach a subscriber over the wire
/// (ADR-0065 clause 3), not through this.
/// </remarks>
/// <param name="audit">The program subscribing, and where its records
/// go.</param>
/// <param name="subscriber">The Party's UUID the program subscribes
/// as.</param>
public sealed class EventFeed(ProgramAudit audit, string subscriber)
{
    // How many Events a follower may hold before the runtime's own queue
    // holds the rest, and counts what it refuses.
    private const int Held = 1024;

    /// <summary>The program subscribing.</summary>
    public ProgramAudit Audit { get; } = audit ?? throw new ArgumentNullException(nameof(audit));

    /// <summary>The Party's UUID it subscribes as.</summary>
    public string Subscriber { get; } =
        subscriber ?? throw new ArgumentNullException(nameof(subscriber));

    /// <summary>
    /// A subscription to drain (<see cref="EventSubscription.Next"/>), which
    /// the caller disposes. <paramref name="capacity"/> bounds its queue, 0
    /// for the runtime's default.
    /// </summary>
    /// <exception cref="UnauthorizedAccessException">The authorization gate
    /// refused the subscriber; the message is its sentence.</exception>
    /// <exception cref="InvalidOperationException">No runtime library could be
    /// loaded.</exception>
    public EventSubscription Subscribe(EventFilter? filter = null, int capacity = 0)
    {
        return RuntimeLibrary.Rules.Events.Subscribe(
            Audit.Program, Audit.Directory, Subscriber, filter, capacity);
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
            Audit.Program, Audit.Directory, Subscriber, heard => Hand(arrived.Writer, heard),
            filter);

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
