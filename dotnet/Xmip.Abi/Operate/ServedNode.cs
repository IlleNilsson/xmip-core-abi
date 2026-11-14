using Xmip.Abi.Module;

namespace Xmip.Abi.Operate;

/// <summary>
/// An operate listener this process serves (section 17), for a program
/// hosting a node through the C ABI: what it posts, and the acts on the
/// nodes of this process, until disposed — every connection shut, every
/// wait woken.
/// </summary>
public sealed class ServedNode : IDisposable
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
            unsafe
            {
                _ = calls.ServedAddress(handle, written, (nuint)text.Length, &length);
            }
        }

        Address = RuntimeRemotes.Said(text, length);
    }

    /// <summary>Where it listens: <c>host:port</c>, or the local endpoint's
    /// path.</summary>
    public string Address { get; }

    /// <summary>Post <paramref name="publication"/>, the text a publication
    /// file holds, as what the listener answers: every surface following is
    /// told when it differs from the last.</summary>
    public void Post(string publication)
    {
        Posted(RemoteFollows.Node, publication);
    }

    /// <summary>Post <paramref name="publication"/>, a cluster's gathered
    /// view as a publication file holds it, as what the listener answers to
    /// a surface asking for the cluster: from the first post it answers one,
    /// every surface following it told when it differs from the last; before
    /// it, it refuses as a node declaring no monitoring role does.</summary>
    public void PostCluster(string publication)
    {
        Posted(RemoteFollows.Cluster, publication);
    }

    private void Posted(RemoteFollows which, string publication)
    {
        ArgumentNullException.ThrowIfNull(publication);

        Utf8Pack pack = new([publication]);
        int status;

        fixed (byte* data = pack.Bytes)
        {
            unsafe
            {
                nint served = Volatile.Read(ref handle);
                status = which == RemoteFollows.Cluster
                    ? calls.PostCluster(served, pack.Borrow(data, 0))
                    : calls.Post(served, pack.Borrow(data, 0));
            }
        }

        if (status != 0)
        {
            throw new InvalidOperationException(
                $"the runtime answered a {which} post with {(XmipStatus)status}");
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        nint held = Interlocked.Exchange(ref handle, 0);

        if (held != 0)
        {
            unsafe
            {
                calls.ServeStop(held);
            }
        }
    }
}
