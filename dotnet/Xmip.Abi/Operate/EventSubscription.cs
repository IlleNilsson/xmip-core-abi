using System.Runtime.InteropServices;

namespace Xmip.Abi.Operate;

/// <summary>
/// An open subscription to the Events of the process that loaded the
/// runtime (section 11 of <c>xmip_operate.h</c>): a queue drained with
/// <see cref="Next"/>, or a callback the runtime calls
/// (<see cref="RuntimeEvents.Listen"/>). Disposing it unsubscribes.
/// </summary>
/// <remarks>
/// A safe handle, so a drain in progress holds it: disposing it while
/// another thread waits in <see cref="Next"/> releases it when that drain
/// returns, never beneath it. A listening subscription disposed from
/// inside its own callback returns at once, as the header allows.
/// </remarks>
public sealed class EventSubscription : SafeHandle
{
    private readonly RuntimeEvents _events;
    private readonly EventListening? _listening;
    private GCHandle _context;

    internal EventSubscription(RuntimeEvents events, EventListening? listening)
        : base(0, ownsHandle: true)
    {
        _events = events;
        _listening = listening;
    }

    /// <inheritdoc />
    public override bool IsInvalid => handle == 0;

    /// <summary>Whether the runtime calls back rather than being
    /// drained.</summary>
    public bool IsListening => _listening is not null;

    /// <summary>The last exception a listening callback threw, caught so it
    /// never crosses into the runtime; null when none has.</summary>
    public Exception? Fault => _listening?.Fault;

    /// <summary>
    /// Up to <paramref name="max"/> Events, waiting up to
    /// <paramref name="timeout"/> for the first and waking the moment one
    /// arrives; copied out, and the runtime's batch freed, before this
    /// returns. <see cref="EventDelivery.Nothing"/> when nothing arrived.
    /// </summary>
    /// <exception cref="InvalidOperationException">The subscription is a
    /// listening one, which is never drained.</exception>
    /// <exception cref="ObjectDisposedException">It was disposed.</exception>
    public EventDelivery Next(TimeSpan timeout, int max)
    {
        return _events.Next(this, timeout, max);
    }

    internal void Adopt(nint subscription, GCHandle context)
    {
        _context = context;
        SetHandle(subscription);
    }

    /// <inheritdoc />
    protected override bool ReleaseHandle()
    {
        _events.Unsubscribe(handle);

        // The runtime calls back no more once unsubscribe returns, and a
        // callback in progress already holds what the context named.
        if (_context.IsAllocated)
        {
            _context.Free();
        }

        return true;
    }
}

/// <summary>What a listening subscription's callback reaches through its
/// context: the program's handler, and the last exception it threw.</summary>
internal sealed class EventListening(Action<EventRecord> heard)
{
    public Action<EventRecord> Heard { get; } = heard;

    public Exception? Fault { get; set; }
}
