using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace Xmip.Surface;

/// <summary>
/// Every watch of one surface, told at once: each <see cref="Watch"/> its own
/// queue, <see cref="Announce"/> written to all of them, and
/// <see cref="Complete"/> ending them all. What a <see cref="ListenerOperator"/>
/// tells its watches through when the runtime's follow tells it.
/// </summary>
internal sealed class ChangeFeed
{
    private readonly List<Channel<SurfaceChange>> watchers = [];

    private readonly Lock gate = new();

    /// <summary>A watch: <paramref name="first"/>, then every change
    /// announced while it lasts, until <paramref name="stop"/> or
    /// <see cref="Complete"/>. <paramref name="began"/> is called once the
    /// watch is listening.</summary>
    public async IAsyncEnumerable<SurfaceChange> Watch(
        Func<SurfaceChange> first, Action began,
        [EnumeratorCancellation] CancellationToken stop = default)
    {
        Channel<SurfaceChange> channel = Channel.CreateUnbounded<SurfaceChange>(
            new UnboundedChannelOptions { SingleReader = true });

        lock (gate)
        {
            watchers.Add(channel);
        }

        began();

        try
        {
            yield return first();

            while (true)
            {
                bool more;

                try
                {
                    more = await channel.Reader.WaitToReadAsync(stop).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // The normal end of a watch.
                    more = false;
                }

                if (!more)
                {
                    yield break;
                }

                while (channel.Reader.TryRead(out SurfaceChange? change))
                {
                    yield return change;
                }
            }
        }
        finally
        {
            lock (gate)
            {
                watchers.Remove(channel);
            }
        }
    }

    /// <summary>Tell every watch <paramref name="change"/>.</summary>
    public void Announce(SurfaceChange change)
    {
        lock (gate)
        {
            foreach (Channel<SurfaceChange> watcher in watchers)
            {
                watcher.Writer.TryWrite(change);
            }
        }
    }

    /// <summary>End every watch.</summary>
    public void Complete()
    {
        lock (gate)
        {
            foreach (Channel<SurfaceChange> watcher in watchers)
            {
                watcher.Writer.TryComplete();
            }
        }
    }
}
