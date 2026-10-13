using Xmip.Abi.Module;

namespace Xmip.Abi.Operate;

/// <summary>
/// An operate listener this process serves (section 17), for a program
/// hosting a node through the C ABI: what it posts, and the acts on the
/// nodes of this process, until disposed — every connection shut, every
/// wait woken.
/// </summary>
public sealed unsafe class ServedNode : IDisposable
{
    private readonly RuntimeRemotes calls;

    private nint handle;

    internal ServedNode(RuntimeRemotes calls, nint handle)
    {
        this.calls = calls;
        this.handle = handle;

        byte[] text = new byte[256];
        nuint length = 0;

        fixed (byte* written = text)
        {
            _ = calls.ServedAddress(handle, written, (nuint)text.Length, &length);
        }

        Address = RuntimeRemotes.Said(text, length);
    }

    /// <summary>Where it listens, <c>host:port</c>.</summary>
    public string Address { get; }

    /// <summary>Post <paramref name="publication"/>, the text a publication
    /// file holds, as what the listener answers: every surface following is
    /// told when it differs from the last.</summary>
    public void Post(string publication)
    {
        ArgumentNullException.ThrowIfNull(publication);

        Utf8Pack pack = new([publication]);
        int status;

        fixed (byte* data = pack.Bytes)
        {
            status = calls.Post(Volatile.Read(ref handle), pack.Borrow(data, 0));
        }

        if (status != 0)
        {
            throw new InvalidOperationException(
                $"the runtime answered a post with {(XmipStatus)status}");
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        nint held = Interlocked.Exchange(ref handle, 0);

        if (held != 0)
        {
            calls.ServeStop(held);
        }
    }
}
