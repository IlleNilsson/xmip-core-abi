using System.Runtime.InteropServices;
using Xmip.Abi.Module;

namespace Xmip.Abi.Operate;

/// <summary>What a remote node's follow has told: how many times, the
/// revision of the publication it follows, whether it is reached, which
/// publication it follows — <see cref="RemoteFollows.Answered"/> while the
/// node's answer has not decided, and while it is lost — and, where it is not
/// reached, why.</summary>
public sealed record RemoteFollow(
    ulong Told, ulong Revision, bool Reachable, RemoteFollows Follows, string Reason);

/// <summary>
/// A node reached at its operate address (section 17), held open until
/// disposed. Any thread may call it at once: a follow's wait on one, a
/// read or an act on another. Disposing ends the follow, wakes every wait,
/// lets every call in flight return and then frees the handle, so nothing
/// crosses after it is freed.
/// </summary>
public sealed class RemoteNode : IDisposable
{
    private readonly RuntimeRemotes calls;

    private readonly nint handle;

    // The keeper a kept open signs with, held until the handle is freed.
    private readonly GCHandle kept;

    private int inFlight;

    private int disposed;

    internal RemoteNode(RuntimeRemotes calls, nint handle, GCHandle kept = default)
    {
        this.calls = calls;
        this.handle = handle;
        this.kept = kept;
    }

    /// <summary>
    /// The node's publication, the text its file holds, and its revision.
    /// <see cref="XmipStatus.Ok"/>; <see cref="XmipStatus.Unavailable"/> with
    /// why in <paramref name="text"/> where the node did not answer.
    /// </summary>
    public XmipStatus Publication(out string text, out ulong revision)
    {
        return Fetched(RemoteFollows.Node, out text, out revision);
    }

    /// <summary>
    /// The cluster's view the node gathers — a node declaring the monitoring
    /// role — the text a publication file holds, and its revision: 0 and
    /// empty until its first view. <see cref="XmipStatus.Ok"/>;
    /// <see cref="XmipStatus.Unsupported"/> with the node's refusal in
    /// <paramref name="text"/> where it gathers none, so its own publication
    /// is the one to follow; <see cref="XmipStatus.Unavailable"/> with why
    /// where it did not answer or refused this caller.
    /// </summary>
    public XmipStatus Cluster(out string text, out ulong revision)
    {
        return Fetched(RemoteFollows.Cluster, out text, out revision);
    }

    /// <summary>What the follow of <paramref name="follows"/> has told once
    /// it has told more than <paramref name="seen"/> times, or after
    /// <paramref name="timeout"/>: following <see cref="RemoteFollows.Answered"/>,
    /// its <see cref="RemoteFollow.Follows"/> says which the node's answer
    /// decided. The follow starts with the first wait, and a handle follows
    /// what its first wait names.</summary>
    /// <exception cref="InvalidOperationException">A later wait named
    /// another: the runtime's refusal, a defect of the caller's.</exception>
    public RemoteFollow Wait(RemoteFollows follows, ulong seen, TimeSpan timeout)
    {
        if (!Enter())
        {
            return new RemoteFollow(seen, 0, false, RemoteFollows.Answered, "disposed");
        }

        try
        {
            byte[] reason = new byte[RuntimeRemotes.Room];
            ulong told = 0;
            ulong revision = 0;
            byte reachable = 0;
            uint followed = (uint)RemoteFollows.Answered;
            nuint length = 0;
            uint waited = (uint)Math.Clamp(timeout.TotalMilliseconds, 0, uint.MaxValue);
            int status;

            fixed (byte* written = reason)
            {
                unsafe
                {
                    status = calls.Wait(
                        handle, (uint)follows, seen, waited, &told, &revision, &reachable,
                        &followed, written, (nuint)reason.Length, &length);
                }
            }

            string why = RuntimeRemotes.Said(reason, length);

            return status == 0
                ? new RemoteFollow(
                    told, revision, reachable != 0, (RemoteFollows)followed, why)
                : throw new InvalidOperationException(
                    $"the runtime answered a remote wait with {(XmipStatus)status}: {why}");
        }
        finally
        {
            Exit();
        }
    }

    /// <summary>
    /// Ask the node to apply <paramref name="act"/> to the
    /// <paramref name="noun"/> called <paramref name="target"/> of the node at
    /// <paramref name="node"/>, as the identity this side's certificate
    /// proves — or, for <paramref name="caller"/>, a caller this process holds,
    /// relayed: the node vouches for it and admits the act by its own role
    /// (section 17; ADR-0009, amendment 2026-11-01); null for this side.
    /// <see cref="XmipStatus.Ok"/> taken; <see cref="XmipStatus.Again"/> asked:
    /// written to Xmip Storage at the node's door and not taken yet, its node
    /// taking it as soon as it runs (ADR-0014, amendment 2026-11-13);
    /// <see cref="XmipStatus.Auth"/> refused by
    /// role, or no one; <see cref="XmipStatus.NotFound"/> refused by the node;
    /// <see cref="XmipStatus.Invalid"/> no such act; <see cref="XmipStatus.Io"/>
    /// Xmip Storage did not answer; <see cref="XmipStatus.Unavailable"/> the
    /// node did not. The sentence in <paramref name="said"/>.
    /// </summary>
    public XmipStatus Act(
        HeldCaller? caller, string node, string noun, string target, string act,
        out string said)
    {
        said = "disposed";

        if (!Enter())
        {
            return XmipStatus.Unavailable;
        }

        try
        {
            Utf8Pack pack = new([node, noun, target, act]);
            byte[] text = new byte[RuntimeRemotes.Room];
            nuint length = 0;
            int status;

            fixed (byte* data = pack.Bytes)
            fixed (byte* written = text)
            {
                unsafe
                {
                    status = calls.Act(
                        handle, caller?.Handle ?? 0, pack.Borrow(data, 0), pack.Borrow(data, 1),
                        pack.Borrow(data, 2), pack.Borrow(data, 3), written, (nuint)text.Length,
                        &length);
                }
            }

            said = RuntimeRemotes.Said(text, length);
            return (XmipStatus)status;
        }
        finally
        {
            Exit();
        }
    }

    /// <summary>
    /// Set the secret <paramref name="name"/> of <paramref name="kind"/> to
    /// <paramref name="value"/> through the node at <paramref name="node"/>
    /// — empty, the one node it runs — section 24: the value crosses inside
    /// the handle's connection and is never said back. The statuses are
    /// <see cref="Act"/>'s; the sentence in <paramref name="said"/>.
    /// </summary>
    public XmipStatus SetSecret(
        HeldCaller? caller, string node, string name, string kind, string value,
        out string said)
    {
        said = "disposed";

        if (!Enter())
        {
            return XmipStatus.Unavailable;
        }

        try
        {
            Utf8Pack pack = new([node, name, kind, value]);
            byte[] text = new byte[RuntimeRemotes.Room];
            nuint length = 0;
            int status;

            fixed (byte* data = pack.Bytes)
            fixed (byte* written = text)
            {
                unsafe
                {
                    status = calls.SetSecret(
                        handle, caller?.Handle ?? 0, pack.Borrow(data, 0), pack.Borrow(data, 1),
                        pack.Borrow(data, 2), pack.Borrow(data, 3), written, (nuint)text.Length,
                        &length);
                }
            }

            // The value was packed beside the rest: overwritten once crossed.
            Array.Clear(pack.Bytes);
            said = RuntimeRemotes.Said(text, length);
            return (XmipStatus)status;
        }
        finally
        {
            Exit();
        }
    }

    /// <summary>
    /// The secrets the node at <paramref name="node"/> keeps — empty, the
    /// one node it runs — as JSON in <paramref name="text"/>, never a value
    /// (section 24); where not <see cref="XmipStatus.Ok"/>, why.
    /// </summary>
    public XmipStatus Secrets(HeldCaller? caller, string node, out string text)
    {
        text = "disposed";

        if (!Enter())
        {
            return XmipStatus.Unavailable;
        }

        try
        {
            Utf8Pack pack = new([node]);
            byte[] read = new byte[64 * 1024];

            while (true)
            {
                nuint length = 0;
                int status;

                fixed (byte* data = pack.Bytes)
                fixed (byte* written = read)
                {
                    unsafe
                    {
                        status = calls.Secrets(
                            handle, caller?.Handle ?? 0, pack.Borrow(data, 0), written,
                            (nuint)read.Length, &length);
                    }
                }

                if (status == (int)XmipStatus.Truncated && length > (nuint)read.Length)
                {
                    read = new byte[(int)length];
                    continue;
                }

                text = RuntimeRemotes.Said(read, length);
                return (XmipStatus)status;
            }
        }
        finally
        {
            Exit();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) == 1)
        {
            return;
        }

        unsafe
        {
            calls.End(handle);
        }

        SpinWait spin = default;

        while (Volatile.Read(ref inFlight) > 0)
        {
            spin.SpinOnce();
        }

        unsafe
        {
            calls.Free(handle);
        }

        if (kept.IsAllocated)
        {
            unsafe
            {
                kept.Free();
            }
        }
    }

    // The node's own publication or the cluster's view it gathers, read
    // whole: the two calls answer alike, but for the cluster's refusal.
    private XmipStatus Fetched(RemoteFollows which, out string text, out ulong revision)
    {
        text = "disposed";
        revision = 0;

        if (!Enter())
        {
            return XmipStatus.Unavailable;
        }

        try
        {
            byte[] read = new byte[64 * 1024];
            int status = Read(which, read, out nuint length, out revision);

            // Too long for the first buffer: the runtime kept it whole.
            if (status == (int)XmipStatus.Truncated)
            {
                read = new byte[(int)length];
                status = Read(which, read, out length, out revision);
            }

            text = RuntimeRemotes.Said(read, length);
            return status switch
            {
                0 or (int)XmipStatus.Unavailable => (XmipStatus)status,
                (int)XmipStatus.Unsupported when which == RemoteFollows.Cluster =>
                    XmipStatus.Unsupported,
                _ => throw new InvalidOperationException(
                    $"the runtime answered a remote {which} read with {(XmipStatus)status}"),
            };
        }
        finally
        {
            Exit();
        }
    }

    private int Read(RemoteFollows which, byte[] read, out nuint length, out ulong revision)
    {
        nuint written = 0;
        ulong at = 0;
        int status;

        fixed (byte* text = read)
        {
            unsafe
            {
                status = which == RemoteFollows.Cluster
                    ? calls.Cluster(handle, text, (nuint)read.Length, &written, &at)
                    : calls.Publication(handle, text, (nuint)read.Length, &written, &at);
            }
        }

        length = written;
        revision = at;
        return status;
    }

    // A call crosses only while the handle lives: counted in before the
    // check, so Dispose sees it and waits for it.
    private bool Enter()
    {
        Interlocked.Increment(ref inFlight);

        if (Volatile.Read(ref disposed) == 0)
        {
            return true;
        }

        Interlocked.Decrement(ref inFlight);
        return false;
    }

    private void Exit()
    {
        Interlocked.Decrement(ref inFlight);
    }
}
