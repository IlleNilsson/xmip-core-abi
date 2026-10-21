using Xmip.Abi.Module;

namespace Xmip.Abi.Operate;

/// <summary>
/// One browser's connection to a web host, its handshake finished by the
/// runtime in Xmip's own TLS (section 21): read on one thread and written
/// on another at once, as a web server pumps it. <see cref="Close"/> from
/// any thread wakes a read waiting; <see cref="Dispose"/> closes it, lets
/// every call in flight return and frees it.
/// </summary>
public sealed unsafe class SecureSurfaceSession : IDisposable
{
    private readonly RuntimeSecureSurface calls;

    private nint handle;

    private int inFlight;

    internal SecureSurfaceSession(RuntimeSecureSurface calls, nint handle)
    {
        this.calls = calls;
        this.handle = handle;

        byte[] room = new byte[16 * 1024];
        nuint length = 0;

        fixed (byte* written = room)
        {
            _ = calls.Peer(handle, written, (nuint)room.Length, &length);
        }

        Presented = room[..(int)Math.Min(length, (nuint)room.Length)];
        Array.Clear(room);

        fixed (byte* written = room)
        {
            _ = calls.Ends(handle, written, (nuint)room.Length, &length);
        }

        string[] ends = RuntimeRemotes.Said(room, length).Split(' ');
        Local = ends[0];
        Remote = ends.Length > 1 ? ends[1] : string.Empty;
    }

    /// <summary>The leaf certificate the browser presented, DER, which the
    /// runtime has verified against the listener's anchors; empty where it
    /// presented none.</summary>
    public byte[] Presented { get; }

    /// <summary>Where the connection was accepted, <c>host:port</c>.</summary>
    public string Local { get; }

    /// <summary>Where the browser is, <c>host:port</c>.</summary>
    public string Remote { get; }

    /// <summary>
    /// What the browser sent next, into <paramref name="buffer"/>, waiting
    /// for it: how many bytes, 0 once the connection has ended, -1 where a
    /// record could not be read.
    /// </summary>
    public int Read(Span<byte> buffer)
    {
        nint held = Enter();

        try
        {
            if (held == 0)
            {
                return 0;
            }

            nuint length = 0;
            int status;

            fixed (byte* into = buffer)
            {
                status = calls.Read(held, into, (nuint)buffer.Length, &length);
            }

            return status == 0 ? (int)length : -1;
        }
        finally
        {
            Interlocked.Decrement(ref inFlight);
        }
    }

    /// <summary>Write <paramref name="bytes"/> whole and send them: false
    /// where the connection has ended.</summary>
    public bool Write(ReadOnlySpan<byte> bytes)
    {
        nint held = Enter();

        try
        {
            if (held == 0)
            {
                return false;
            }

            fixed (byte* from = bytes)
            {
                return calls.Write(held, new XmipStr(from, (nuint)bytes.Length)) == 0;
            }
        }
        finally
        {
            Interlocked.Decrement(ref inFlight);
        }
    }

    /// <summary>Close: the browser told where no write holds the connection,
    /// the socket shut both ways, a read waiting woken to its end. From any
    /// thread.</summary>
    public void Close()
    {
        nint held = Enter();

        try
        {
            if (held != 0)
            {
                calls.Close(held);
            }
        }
        finally
        {
            Interlocked.Decrement(ref inFlight);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // Taken first, so no call begun after sees it; one begun before is
        // counted, and woken by the close, and the handle is freed once each
        // has returned.
        nint held = Interlocked.Exchange(ref handle, 0);

        if (held == 0)
        {
            return;
        }

        calls.Close(held);
        SpinWait waiting = default;

        while (Volatile.Read(ref inFlight) > 0)
        {
            waiting.SpinOnce();
        }

        calls.SessionFree(held);
    }

    // The handle, counted as a call in flight so it is never freed under the
    // call; 0 once it is disposed. Each call leaves by decrementing.
    private nint Enter()
    {
        Interlocked.Increment(ref inFlight);
        return Volatile.Read(ref handle);
    }
}