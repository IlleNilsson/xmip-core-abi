using System.Runtime.InteropServices;
using System.Text;
using Xmip.Abi.Module;

namespace Xmip.Abi.Operate;

/// <summary>
/// Section 15 of <c>include/xmip_operate.h</c>, crossed by P/Invoke
/// (ADR-0052, amendment 2026-10-01): the Dead Message Queues of every node
/// running in this process, and Replay on one of their Messages. The queue,
/// the Replay, its word and its audit are the runtime's and
/// <c>observe</c>'s; this binds each call once and decides none of it. The
/// same Replay left for a node a surface reads through its publication is
/// <see cref="RuntimeSubscriptions.Order"/>, the one order for every noun.
/// </summary>
/// <remarks>Pure in the header's sense: one instance serves a whole process
/// from any thread.</remarks>
public sealed unsafe class RuntimeDeadMessages
{
    // A sentence fits in this; a list is asked for again at its true length.
    private const int Room = 16 * 1024;

    private readonly delegate* unmanaged[Cdecl]<XmipStr, byte*, nuint, nuint*, int> _standing;
    private readonly delegate* unmanaged[Cdecl]<
        XmipStr, XmipStr, XmipStr, byte*, nuint, nuint*, int> _replay;

    internal RuntimeDeadMessages(nint library)
    {
        _standing = (delegate* unmanaged[Cdecl]<XmipStr, byte*, nuint, nuint*, int>)
            NativeLibrary.GetExport(library, OperateAbi.DeadMessagesEntrypoint);
        _replay = (delegate* unmanaged[Cdecl]<
            XmipStr, XmipStr, XmipStr, byte*, nuint, nuint*, int>)
            NativeLibrary.GetExport(library, OperateAbi.DeadMessageReplayEntrypoint);
    }

    /// <summary>The symbols this binds, each of which a runtime must export;
    /// the publication's list is <see cref="PublicationReader"/>'s.</summary>
    public static IReadOnlyList<string> Entrypoints { get; } =
    [
        OperateAbi.DeadMessagesEntrypoint,
        OperateAbi.DeadMessageReplayEntrypoint,
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

    /// <summary>
    /// Replay the Message <paramref name="message"/> from the Dead Message
    /// Queue of the node at <paramref name="node"/> in this process, by
    /// <paramref name="who"/>. <see cref="XmipStatus.Ok"/> with what came of
    /// it in <paramref name="said"/> — a Message replayed before is said so,
    /// and not replayed twice; <see cref="XmipStatus.NotFound"/> with the
    /// refusal, opening REFUSED, when the node is not here, its queue never
    /// kept the Message, or it still matches nothing;
    /// <see cref="XmipStatus.Io"/>, opening
    /// FAILED, when Xmip Storage did not answer and nothing changed.
    /// </summary>
    public XmipStatus Replay(string node, string message, string who, out string said)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(who);

        Utf8Pack pack = new([node, message, who]);
        byte[] text = new byte[Room];
        nuint length = 0;
        int status;

        fixed (byte* data = pack.Bytes)
        fixed (byte* written = text)
        {
            status = _replay(
                pack.Borrow(data, 0), pack.Borrow(data, 1), pack.Borrow(data, 2), written,
                (nuint)text.Length, &length);
        }

        said = Encoding.UTF8.GetString(text, 0, (int)Math.Min(length, (nuint)text.Length));

        return RuntimeSubscriptions.Answered(status, "a Replay from a Dead Message Queue");
    }

    private int Listed(Utf8Pack pack, byte[] text, ref nuint needed)
    {
        fixed (byte* data = pack.Bytes)
        fixed (byte* written = text)
        fixed (nuint* length = &needed)
        {
            return _standing(pack.Borrow(data, 0), written, (nuint)text.Length, length);
        }
    }
}
