using System.Runtime.InteropServices;
using System.Text;
using Xmip.Abi.Module;

namespace Xmip.Abi.Operate;

/// <summary>
/// Section 16 of <c>include/xmip_operate.h</c>, crossed by P/Invoke
/// (runtime-model.md section 13; ADR-0013): Retry and Dismiss on a Journey
/// that failed, sent by a node running in this process. The Journey, the
/// act, its word and its audit are the runtime's and <c>observe</c>'s; this
/// binds the call once and decides none of it. The same act left for a node
/// a surface reads through its publication is
/// <see cref="RuntimeSubscriptions.Order"/>, the one order for every noun.
/// </summary>
/// <remarks>Pure in the header's sense: one instance serves a whole process
/// from any thread.</remarks>
public sealed unsafe class RuntimeJourneys
{
    // A sentence fits in this.
    private const int Room = 16 * 1024;

    private readonly delegate* unmanaged[Cdecl]<
        XmipStr, XmipStr, XmipStr, XmipStr, byte*, nuint, nuint*, int> _act;

    internal RuntimeJourneys(nint library)
    {
        _act = (delegate* unmanaged[Cdecl]<
            XmipStr, XmipStr, XmipStr, XmipStr, byte*, nuint, nuint*, int>)
            NativeLibrary.GetExport(library, OperateAbi.JourneyActEntrypoint);
    }

    /// <summary>The symbols this binds, each of which a runtime must
    /// export.</summary>
    public static IReadOnlyList<string> Entrypoints { get; } = [OperateAbi.JourneyActEntrypoint];

    /// <summary>
    /// Apply <paramref name="act"/> — <c>retry</c> or <c>dismiss</c>, exact —
    /// to the Journey <paramref name="journey"/> sent by the node at
    /// <paramref name="node"/> in this process, by <paramref name="who"/>.
    /// <see cref="XmipStatus.Ok"/> with what came of it in
    /// <paramref name="said"/>; <see cref="XmipStatus.NotFound"/> with the
    /// refusal, opening REFUSED, when the node is not here, the Ledger holds
    /// no such Journey, it has not failed, or the node does not send its Send
    /// Port; <see cref="XmipStatus.Invalid"/> for a word that is no act on a
    /// Journey; <see cref="XmipStatus.Io"/>, opening FAILED, when Xmip Storage
    /// did not answer and nothing changed.
    /// </summary>
    public XmipStatus Act(string node, string journey, string act, string who, out string said)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(journey);
        ArgumentNullException.ThrowIfNull(act);
        ArgumentNullException.ThrowIfNull(who);

        Utf8Pack pack = new([node, journey, act, who]);
        byte[] text = new byte[Room];
        nuint length = 0;
        int status;

        fixed (byte* data = pack.Bytes)
        fixed (byte* written = text)
        {
            status = _act(
                pack.Borrow(data, 0), pack.Borrow(data, 1), pack.Borrow(data, 2),
                pack.Borrow(data, 3), written, (nuint)text.Length, &length);
        }

        said = Encoding.UTF8.GetString(text, 0, (int)Math.Min(length, (nuint)text.Length));

        return RuntimeSubscriptions.Answered(status, "an act on a Journey");
    }
}
