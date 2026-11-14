using Xmip.Abi.Module;
using Xmip.Abi.Operate;

namespace Xmip.Surface;

/// <summary>
/// What a listener follows and how it reads it. It follows what the runtime
/// calls answered (<see cref="RemoteFollows.Answered"/>): the cluster's view
/// where the node it reaches gathers one — a node declaring the monitoring
/// role — and the node's own publication where the node refuses, its answer
/// deciding as each connection to it is made, in the runtime and never by a
/// name (the design approved 2026-11-12, section 3.4). This reads what the
/// follow says it follows, and nothing while the node's answer has not
/// decided; where it follows the cluster it answers for the whole cluster.
/// </summary>
/// <remarks>
/// It reads what it follows — the text a publication file holds, read by the
/// one reader (<see cref="PublicationReading"/>) — once per revision, and is
/// told, never asks, when it advances: the runtime's follow holds the node's
/// wait. A watch is told first and the publication read after, by the first
/// question that needs it, off the follow: until 2026-10-13 the follow read
/// and parsed the whole publication before it told, and a .NET watch heard
/// of a change 1.7 ms after the node posted it. A lost node is told too:
/// every watch at once, with a <see cref="Source"/> that says the node is
/// unreachable and why, and every answer is empty until it is back — never
/// the publication held from before. The follow tries a lost node again on
/// its own loop (ADR-0052, note of 2026-10-05), in the runtime, and decides
/// afresh what it follows as it reconnects.
/// </remarks>
public abstract partial class ListenerOperator
{
    /// <summary>How long a watch's one wait lasts before it looks whether
    /// the watch ended; a change wakes it at once.</summary>
    private static readonly TimeSpan Turn = TimeSpan.FromSeconds(1);

    // How long a question waits for the follow's first telling, which says
    // what the node's answer decided. The runtime tells at once whether the
    // node answered or not; this bounds only a node that does neither.
    private static readonly TimeSpan Deciding = TimeSpan.FromSeconds(30);

    // One read of the node at a time, so a change told is read once.
    private readonly Lock reading = new();

    private PublicationReading? held;

    private ulong heldRevision;

    // The revision the follow last told.
    private ulong toldRevision;

    private Thread? following;

    private volatile bool ended;

    // What the follow last said it follows, a RemoteFollows: Answered until
    // the node's answer decides, and while the node is lost.
    private int follows = (int)RemoteFollows.Answered;

    // What the gathered view says of itself, which Source says; empty for a
    // node's own publication.
    private string gathered = string.Empty;

    /// <summary>Whether the node this reaches gathers its cluster's view, so
    /// that this follows that view and answers for the whole cluster: false
    /// until the node's answer decides, while it is lost, and where it
    /// gathers none.</summary>
    public bool Gathers => Volatile.Read(ref follows) == (int)RemoteFollows.Cluster;

    private string Gathered => Volatile.Read(ref gathered);

    /// <summary>End every watch, the follow and the node's handle.</summary>
    protected virtual void Dispose(bool disposing)
    {
        if (!disposing || ended)
        {
            return;
        }

        ended = true;
        feed.Complete();
        node?.Dispose();
        following?.Join();
    }

    // The follow starts with the first watch and runs while this lives.
    private void Followed()
    {
        lock (gate)
        {
            if (node is not null && following is null)
            {
                following = new Thread(Follow) { IsBackground = true, Name = "xmip-operate" };
                following.Start();
            }
        }
    }

    // What this follows, read once per revision the follow told, by the
    // first question after it told; read on every question where nothing
    // follows. Unreachable, nothing — never what was held from before.
    private PublicationReading Read()
    {
        if (node is null)
        {
            IsConnected = false;
            return PublicationReading.Nothing(Source);
        }

        lock (reading)
        {
            lock (gate)
            {
                if (following is not null && held is { } current && IsConnected)
                {
                    return current;
                }
            }

            XmipStatus status = Fetch(node, out string text, out ulong revision);

            if (status != XmipStatus.Ok)
            {
                IsConnected = false;
                Reason = text;
                return PublicationReading.Nothing(Source);
            }

            IsConnected = true;
            Reason = string.Empty;

            lock (gate)
            {
                if (held is { } same && heldRevision == revision)
                {
                    return same;
                }

                PublicationReading read = PublicationReading.Of(text, revision, Source);
                Volatile.Write(ref gathered, Gathers ? read.Said : string.Empty);

                // A reading older than what the follow has since told is
                // answered, not kept: the next question reads the newer.
                if (revision >= Volatile.Read(ref toldRevision))
                {
                    held = read;
                    heldRevision = revision;
                }

                return read;
            }
        }
    }

    // What the follow says this follows, read: where it has said nothing yet,
    // its telling is waited for first — the runtime tells at once whether the
    // node answered — and while the node's answer has not decided, nothing,
    // why in text.
    private XmipStatus Fetch(RemoteNode reached, out string text, out ulong revision)
    {
        if (Volatile.Read(ref follows) == (int)RemoteFollows.Answered)
        {
            RemoteFollow told = reached.Wait(RemoteFollows.Answered, 0, Deciding);
            Volatile.Write(ref follows, (int)told.Follows);

            if (told.Follows == RemoteFollows.Answered)
            {
                text = told.Reason;
                revision = 0;
                return XmipStatus.Unavailable;
            }
        }

        return Volatile.Read(ref follows) == (int)RemoteFollows.Cluster
            ? reached.Cluster(out text, out revision)
            : reached.Publication(out text, out revision);
    }

    // The runtime's follow, waited on: each time it tells, what it follows is
    // taken from it, the reading held goes and every watch is told at once;
    // it is read by whoever asks next, never before the telling.
    private void Follow()
    {
        ulong seen = 0;

        while (!ended && node is not null)
        {
            RemoteFollow told = node.Wait(RemoteFollows.Answered, seen, Turn);

            if (ended || told.Told == seen)
            {
                continue;
            }

            seen = told.Told;

            lock (gate)
            {
                Volatile.Write(ref follows, (int)told.Follows);
                Volatile.Write(ref toldRevision, told.Revision);
                held = null;

                // The gathered view's words go with it; the next read of it
                // says them again.
                if (told.Follows != RemoteFollows.Cluster)
                {
                    Volatile.Write(ref gathered, string.Empty);
                }
            }

            IsConnected = told.Reachable;
            Reason = told.Reachable ? string.Empty : told.Reason;

            feed.Announce(new SurfaceChange(
                told.Revision, SurfaceChangeKind.All, DateTimeOffset.UtcNow, Source));
        }
    }
}
