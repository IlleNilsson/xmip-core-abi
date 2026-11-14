using System.Runtime.InteropServices;
using Xmip.Abi.Module;

namespace Xmip.Abi.Operate;

/// <summary>
/// Section 15 of <c>include/xmip_operate.h</c>, crossed by P/Invoke
/// (ADR-0052, amendment 2026-10-01): the Dead Message Queues of every node
/// running in this process. The queue is the runtime's; this binds the call
/// once and decides none of it. A Replay is asked at a node's door through
/// <see cref="RemoteNode.Act"/> and Xmip Storage (ADR-0014, amendment
/// 2026-11-13).
/// </summary>
/// <remarks>Pure in the header's sense: one instance serves a whole process
/// from any thread.</remarks>
public sealed class RuntimeDeadMessages
{
    // A list fits in this, or is asked for again at its true length.
    private const int Room = 16 * 1024;

    private readonly delegate* unmanaged[Cdecl]<XmipStr, byte*, nuint, nuint*, int> _standing;

    internal RuntimeDeadMessages(nint library)
    {
        _standing = (delegate* unmanaged[Cdecl]<XmipStr, byte*, nuint, nuint*, int>)
            NativeLibrary.GetExport(library, OperateAbi.DeadMessagesEntrypoint);
    }

    /// <summary>The symbols this binds, each of which a runtime must export;
    /// the publication's list is <see cref="PublicationReader"/>'s.</summary>
    public static IReadOnlyList<string> Entrypoints { get; } =
    [
        OperateAbi.DeadMessagesEntrypoint,
    ];

    /// <summary>What the Dead Message Queue of every node running in this
    /// process at or beneath <paramref name="node"/> keeps; empty for every
    /// one.</summary>
    public DeadMessageList Standing(string node)
    {
        ArgumentNullException.ThrowIfNull(node);

        Utf8Pack pack = new([node]);
        byte[] text = new byte[Room];
        nuint needed = 0;
        int status = Listed(pack, text, ref needed);

        if (needed > (nuint)text.Length)
        {
            text = new byte[(int)needed];
            status = Listed(pack, text, ref needed);
        }

        return status == 0
            ? DeadMessageList.Parse(text.AsMemory(0, (int)needed))
            : throw new InvalidOperationException(
                $"the runtime answered the Dead Message Queues with {(XmipStatus)status}");
    }

    private int Listed(Utf8Pack pack, byte[] text, ref nuint needed)
    {
        fixed (byte* data = pack.Bytes)
        fixed (byte* written = text)
        fixed (nuint* length = &needed)
        {
            unsafe
            {
                return _standing(pack.Borrow(data, 0), written, (nuint)text.Length, length);
            }
        }
    }
}
