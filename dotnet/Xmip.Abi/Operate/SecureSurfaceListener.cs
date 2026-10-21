using Xmip.Abi.Module;

namespace Xmip.Abi.Operate;

/// <summary>
/// A web host's HTTPS listener in Xmip's own TLS (section 21): each
/// browser's handshake finished by the runtime, a browser whose certificate
/// the anchors refuse told and never accepted, one presenting none let
/// through. <see cref="Stop"/> from any thread wakes every
/// <see cref="Accept"/>; <see cref="Dispose"/> stops it, lets every accept
/// in flight return and frees it. A session it handed over stays its
/// holder's.
/// </summary>
public sealed unsafe class SecureSurfaceListener : IDisposable
{
    private readonly RuntimeSecureSurface calls;

    private nint handle;

    private int inFlight;

    internal SecureSurfaceListener(RuntimeSecureSurface calls, nint handle)
    {
        this.calls = calls;
        this.handle = handle;

        byte[] text = new byte[256];
        nuint length = 0;

        fixed (byte* written = text)
        {
            _ = calls.Address(handle, written, (nuint)text.Length, &length);
        }

        Address = RuntimeRemotes.Said(text, length);
    }

    /// <summary>Where it listens, <c>host:port</c>, the port a bind to port 0
    /// was given included.</summary>
    public string Address { get; }

    /// <summary>
    /// The next connection whose handshake finished, waiting until there is
    /// one: a session, or a handshake that did not finish — refused by the
    /// anchors, or for any other reason — which never ends the listener, or
    /// <see cref="SecureSurfaceAccepted.Stopped"/> once it is stopped or disposed.
    /// </summary>
    public SecureSurfaceAccepted Accept()
    {
        Interlocked.Increment(ref inFlight);

        try
        {
            nint held = Volatile.Read(ref handle);

            if (held == 0)
            {
                return new SecureSurfaceAccepted(null, false, true, string.Empty);
            }

            byte[] report = new byte[RuntimeRemotes.Room];
            nuint length = 0;
            nint session = 0;
            int status;

            fixed (byte* written = report)
            {
                status = calls.Accept(held, &session, written, (nuint)report.Length, &length);
            }

            string said = RuntimeRemotes.Said(report, length);

            return status switch
            {
                0 when session != 0 => new SecureSurfaceAccepted(
                    new SecureSurfaceSession(calls, session), false, false, string.Empty),
                (int)XmipStatus.Cancelled => new SecureSurfaceAccepted(null, false, true, said),
                (int)XmipStatus.Auth => new SecureSurfaceAccepted(null, true, false, said),
                _ => new SecureSurfaceAccepted(null, false, false, said),
            };
        }
        finally
        {
            Interlocked.Decrement(ref inFlight);
        }
    }

    /// <summary>Stop: nothing accepted after, every accept waiting woken. From
    /// any thread.</summary>
    public void Stop()
    {
        Interlocked.Increment(ref inFlight);

        try
        {
            nint held = Volatile.Read(ref handle);

            if (held != 0)
            {
                calls.Stop(held);
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
        // Taken first, so no call begun after sees it; a call begun before
        // is counted, and the handle is freed once each has returned. A
        // stopped listener answers every accept at once.
        nint held = Interlocked.Exchange(ref handle, 0);

        if (held == 0)
        {
            return;
        }

        calls.Stop(held);
        SpinWait waiting = default;

        while (Volatile.Read(ref inFlight) > 0)
        {
            waiting.SpinOnce();
        }

        calls.Free(held);
    }
}
