namespace Xmip.Abi.Operate;

/// <summary>
/// A caller the runtime proved, held for this host behind an opaque handle
/// (section 18; ADR-0009, amendment 2026-11-01): what a host carries to the
/// gate in place of a name, so no name a host passes is ever a caller. Each
/// holder has one of its own and disposes it, after which it is no one; a
/// holder that outlives the one it came from — a browser's circuit, which
/// outlives the connection that proved it — takes its own with
/// <see cref="Hold"/>. The runtime ends it, too, past the certificate that
/// proved it and when the listener whose session proved it stops.
/// </summary>
public sealed class HeldCaller : IDisposable
{
    private readonly RuntimeSecurityRoles calls;

    private ulong handle;

    internal HeldCaller(RuntimeSecurityRoles calls, ulong handle, string name)
    {
        this.calls = calls;
        this.handle = handle;
        Name = name;
    }

    /// <summary>The handle the gate takes back; 0, no one, once disposed.</summary>
    public ulong Handle => Volatile.Read(ref handle);

    /// <summary>Who the runtime proved the caller is, as its authentication
    /// gate answered: to show, and for a directory to look a role up by;
    /// never a caller itself.</summary>
    public string Name { get; }

    /// <summary>Another holder of this caller: a handle of its own, for as
    /// long and revoked by the same; null where this one is no one.</summary>
    public HeldCaller? Hold()
    {
        return calls.Hold(Handle);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        ulong held = Interlocked.Exchange(ref handle, 0);

        if (held != 0)
        {
            calls.Release(held);
        }
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return Name;
    }
}
