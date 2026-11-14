using System.Runtime.InteropServices;
using System.Text;
using Xmip.Abi.Module;

namespace Xmip.Abi.Operate;

/// <summary>
/// Section 16 of <c>include/xmip_operate.h</c>, crossed by P/Invoke
/// (runtime-model.md section 13; ADR-0013): the Messages that failed at the
/// Send Ports of the nodes running in this process, read from Xmip Storage a
/// page at a time. The Message is the runtime's; this binds the call once
/// and decides none of it. Retry and Dismiss are asked at a node's door
/// through <see cref="RemoteNode.Act"/> and Xmip Storage (ADR-0014,
/// amendment 2026-11-13).
/// </summary>
/// <remarks>Pure in the header's sense: one instance serves a whole process
/// from any thread.</remarks>
public sealed class RuntimeMessages
{
    // A sentence fits in this; a list is asked for again at its true length.
    private const int Room = 16 * 1024;

    private readonly delegate* unmanaged[Cdecl]<
        XmipStr, XmipStr, ulong, uint, byte*, nuint, nuint*, int> _failed;

    internal RuntimeMessages(nint library)
    {
        _failed = (delegate* unmanaged[Cdecl]<
            XmipStr, XmipStr, ulong, uint, byte*, nuint, nuint*, int>)
            NativeLibrary.GetExport(library, OperateAbi.FailedMessagesEntrypoint);
    }

    /// <summary>The symbols this binds, each of which a runtime must
    /// export; the publication's list is <see cref="PublicationReader"/>'s.</summary>
    public static IReadOnlyList<string> Entrypoints { get; } =
    [
        OperateAbi.FailedMessagesEntrypoint,
    ];

    /// <summary>
    /// The Messages that failed at the Send Port <paramref name="port"/>
    /// (empty: every one) of every node running in this process at or
    /// beneath <paramref name="node"/> (empty: every one), read from Xmip
    /// Storage from the place <paramref name="from"/> on, at most
    /// <paramref name="most"/> of each Port (0: a hundred).
    /// </summary>
    /// <exception cref="InvalidOperationException">Xmip Storage did not
    /// answer, in the runtime's words.</exception>
    public FailedMessageList Failed(string node, string port, ulong from, uint most)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(port);

        Utf8Pack pack = new([node, port]);
        byte[] text = new byte[Room];
        nuint needed = 0;
        int status = Listed(pack, from, most, text, ref needed);

        if (needed > (nuint)text.Length)
        {
            text = new byte[(int)needed];
            status = Listed(pack, from, most, text, ref needed);
        }

        return status == 0
            ? FailedMessageList.Parse(text.AsMemory(0, (int)needed))
            : throw new InvalidOperationException(
                Encoding.UTF8.GetString(text, 0, (int)Math.Min(needed, (nuint)text.Length)));
    }

    private int Listed(Utf8Pack pack, ulong from, uint most, byte[] text, ref nuint needed)
    {
        fixed (byte* data = pack.Bytes)
        fixed (byte* written = text)
        fixed (nuint* length = &needed)
        {
            unsafe
            {
                return _failed(
                    pack.Borrow(data, 0), pack.Borrow(data, 1), from, most, written,
                    (nuint)text.Length, length);
            }
        }
    }
}
