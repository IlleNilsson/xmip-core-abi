using Xmip.Abi.Module;

namespace Xmip.Abi.Operate;

/// <summary>What a remote node's follow has told: how many times, the
/// revision of its publication, whether it is reached and, where it is not,
/// why.</summary>
public sealed record RemoteFollow(ulong Told, ulong Revision, bool Reachable, string Reason);

/// <summary>
/// A node reached at its operate address (section 17), held open until
/// disposed. Any thread may call it at once: a follow's wait on one, a
/// read or an act on another. Disposing ends the follow, wakes every wait,
/// lets every call in flight return and then frees the handle, so nothing
/// crosses after it is freed.
/// </summary>
public sealed unsafe class RemoteNode : IDisposable
{
    private readonly RuntimeRemotes calls;

    private readonly nint handle;

    private int inFlight;

    private int disposed;

    internal RemoteNode(RuntimeRemotes calls, nint handle)
    {
        this.calls = calls;
        this.handle = handle;
    }

    /// <summary>
    /// The node's publication, the text its file holds, and its revision.
    /// <see cref="XmipStatus.Ok"/>; <see cref="XmipStatus.Unavailable"/> with
    /// why in <paramref name="text"/> where the node did not answer.
    /// </summary>
    public XmipStatus Publication(out string text, out ulong revision)
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
            int status = Read(read, out nuint length, out revision);

            // Too long for the first buffer: the runtime kept it whole.
            if (status == (int)XmipStatus.Truncated)
            {
                read = new byte[(int)length];
                status = Read(read, out length, out revision);
            }

            text = RuntimeRemotes.Said(read, length);
            return status switch
            {
                0 or (int)XmipStatus.Unavailable => (XmipStatus)status,
                _ => throw new InvalidOperationException(
                    $"the runtime answered a remote publication with {(XmipStatus)status}"),
            };
        }
        finally
        {
            Exit();
        }
    }

    /// <summary>What the follow has told once it has told more than
    /// <paramref name="seen"/> times, or after <paramref name="timeout"/>.
    /// The follow starts with the first wait.</summary>
    public RemoteFollow Wait(ulong seen, TimeSpan timeout)
    {
        if (!Enter())
        {
            return new RemoteFollow(seen, 0, false, "disposed");
        }

        try
        {
            byte[] reason = new byte[RuntimeRemotes.Room];
            ulong told = 0;
            ulong revision = 0;
            byte reachable = 0;
            nuint length = 0;
            uint waited = (uint)Math.Clamp(timeout.TotalMilliseconds, 0, uint.MaxValue);

            fixed (byte* written = reason)
            {
                _ = calls.Wait(
                    handle, seen, waited, &told, &revision, &reachable, written,
                    (nuint)reason.Length, &length);
            }

            return new RemoteFollow(
                told, revision, reachable != 0, RuntimeRemotes.Said(reason, length));
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
    /// proves. <see cref="XmipStatus.Ok"/> taken; <see cref="XmipStatus.Auth"/>
    /// refused by role; <see cref="XmipStatus.NotFound"/> refused by the node;
    /// <see cref="XmipStatus.Invalid"/> no such act; <see cref="XmipStatus.Io"/>
    /// Xmip Storage did not answer; <see cref="XmipStatus.Unavailable"/> the
    /// node did not. The sentence in <paramref name="said"/>.
    /// </summary>
    public XmipStatus Act(
        string node, string noun, string target, string act, out string said)
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
                status = calls.Act(
                    handle, pack.Borrow(data, 0), pack.Borrow(data, 1), pack.Borrow(data, 2),
                    pack.Borrow(data, 3), written, (nuint)text.Length, &length);
            }

            said = RuntimeRemotes.Said(text, length);
            return (XmipStatus)status;
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

        calls.End(handle);

        SpinWait spin = default;

        while (Volatile.Read(ref inFlight) > 0)
        {
            spin.SpinOnce();
        }

        calls.Free(handle);
    }

    private int Read(byte[] read, out nuint length, out ulong revision)
    {
        nuint written = 0;
        ulong at = 0;
        int status;

        fixed (byte* text = read)
        {
            status = calls.Publication(handle, text, (nuint)read.Length, &written, &at);
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
