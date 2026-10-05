using System.Runtime.InteropServices;
using System.Text;
using Xmip.Abi.Module;

namespace Xmip.Abi.Operate;

/// <summary>
/// Section 16 of <c>include/xmip_operate.h</c>, crossed by P/Invoke
/// (runtime-model.md section 13; ADR-0013): the Journeys that failed at the
/// Send Ports of the nodes running in this process, read from Xmip Storage a
/// page at a time, and Retry and Dismiss on one. The Journey, the
/// act, its word and its audit are the runtime's and <c>observe</c>'s; this
/// binds the call once and decides none of it. The same act left for a node
/// a surface reads through its publication is
/// <see cref="RuntimeSubscriptions.Order"/>, the one order for every noun.
/// </summary>
/// <remarks>Pure in the header's sense: one instance serves a whole process
/// from any thread.</remarks>
public sealed unsafe class RuntimeJourneys
{
    // A sentence fits in this; a list is asked for again at its true length.
    private const int Room = 16 * 1024;

    private readonly delegate* unmanaged[Cdecl]<
        XmipStr, XmipStr, XmipStr, XmipStr, byte*, nuint, nuint*, int> _act;

    private readonly delegate* unmanaged[Cdecl]<
        XmipStr, XmipStr, ulong, uint, byte*, nuint, nuint*, int> _failed;

    internal RuntimeJourneys(nint library)
    {
        _act = (delegate* unmanaged[Cdecl]<
            XmipStr, XmipStr, XmipStr, XmipStr, byte*, nuint, nuint*, int>)
            NativeLibrary.GetExport(library, OperateAbi.JourneyActEntrypoint);
        _failed = (delegate* unmanaged[Cdecl]<
            XmipStr, XmipStr, ulong, uint, byte*, nuint, nuint*, int>)
            NativeLibrary.GetExport(library, OperateAbi.FailedJourneysEntrypoint);
    }

    /// <summary>The symbols this binds, each of which a runtime must
    /// export; the publication's list is <see cref="PublicationReader"/>'s.</summary>
    public static IReadOnlyList<string> Entrypoints { get; } =
    [
        OperateAbi.JourneyActEntrypoint,
        OperateAbi.FailedJourneysEntrypoint,
    ];

    /// <summary>
    /// The Journeys that failed at the Send Port <paramref name="port"/>
    /// (empty: every one) of every node running in this process at or
    /// beneath <paramref name="node"/> (empty: every one), read from Xmip
    /// Storage from the place <paramref name="from"/> on, at most
    /// <paramref name="most"/> of each Port (0: a hundred).
    /// </summary>
    /// <exception cref="InvalidOperationException">Xmip Storage did not
    /// answer, in the runtime's words.</exception>
    public FailedJourneyList Failed(string node, string port, ulong from, uint most)
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
            ? FailedJourneyList.Parse(text.AsMemory(0, (int)needed))
            : throw new InvalidOperationException(
                Encoding.UTF8.GetString(text, 0, (int)Math.Min(needed, (nuint)text.Length)));
    }

    private int Listed(Utf8Pack pack, ulong from, uint most, byte[] text, ref nuint needed)
    {
        fixed (byte* data = pack.Bytes)
        fixed (byte* written = text)
        fixed (nuint* length = &needed)
        {
            return _failed(
                pack.Borrow(data, 0), pack.Borrow(data, 1), from, most, written,
                (nuint)text.Length, length);
        }
    }

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
