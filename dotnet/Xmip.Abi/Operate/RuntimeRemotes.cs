using System.Runtime.InteropServices;
using System.Text;
using Xmip.Abi.Module;

namespace Xmip.Abi.Operate;

/// <summary>
/// Section 17 of <c>include/xmip_operate.h</c>, crossed by P/Invoke
/// (ADR-0014, amendment 2026-10-12): a node reached at its operate address
/// over gRPC inside Xmip's mutual TLS — its publication read, its follow
/// waited on, an act asked of it — and this process's node served on an
/// operate listener of its own. The connection, the follow and its
/// recovery loop, the gate and every word are the runtime's; this binds
/// each call once and decides none of it.
/// </summary>
public sealed unsafe class RuntimeRemotes
{
    // A sentence fits in this; a publication is asked for again whole.
    internal const int Room = 16 * 1024;

    private readonly delegate* unmanaged[Cdecl]<
        XmipStr, XmipStr, XmipStr, XmipStr, nint*, byte*, nuint, nuint*, int> _open;
    private readonly delegate* unmanaged[Cdecl]<
        XmipStr, XmipStr, XmipStr, XmipStr, XmipStr, XmipStr, nint*, byte*, nuint, nuint*, int>
        _serve;

    internal RuntimeRemotes(nint library)
    {
        _open = (delegate* unmanaged[Cdecl]<
            XmipStr, XmipStr, XmipStr, XmipStr, nint*, byte*, nuint, nuint*, int>)
            NativeLibrary.GetExport(library, OperateAbi.RemoteOpenEntrypoint);
        Publication = (delegate* unmanaged[Cdecl]<nint, byte*, nuint, nuint*, ulong*, int>)
            NativeLibrary.GetExport(library, OperateAbi.RemotePublicationEntrypoint);
        Wait = (delegate* unmanaged[Cdecl]<
            nint, ulong, uint, ulong*, ulong*, byte*, byte*, nuint, nuint*, int>)
            NativeLibrary.GetExport(library, OperateAbi.RemoteWaitEntrypoint);
        Act = (delegate* unmanaged[Cdecl]<
            nint, XmipStr, XmipStr, XmipStr, XmipStr, byte*, nuint, nuint*, int>)
            NativeLibrary.GetExport(library, OperateAbi.RemoteActEntrypoint);
        End = (delegate* unmanaged[Cdecl]<nint, void>)
            NativeLibrary.GetExport(library, OperateAbi.RemoteEndEntrypoint);
        Free = (delegate* unmanaged[Cdecl]<nint, void>)
            NativeLibrary.GetExport(library, OperateAbi.RemoteFreeEntrypoint);
        _serve = (delegate* unmanaged[Cdecl]<
            XmipStr, XmipStr, XmipStr, XmipStr, XmipStr, XmipStr, nint*, byte*, nuint, nuint*,
            int>)
            NativeLibrary.GetExport(library, OperateAbi.OperateServeEntrypoint);
        ServedAddress = (delegate* unmanaged[Cdecl]<nint, byte*, nuint, nuint*, int>)
            NativeLibrary.GetExport(library, OperateAbi.OperateServedAddressEntrypoint);
        Post = (delegate* unmanaged[Cdecl]<nint, XmipStr, int>)
            NativeLibrary.GetExport(library, OperateAbi.OperatePostEntrypoint);
        ServeStop = (delegate* unmanaged[Cdecl]<nint, void>)
            NativeLibrary.GetExport(library, OperateAbi.OperateServeStopEntrypoint);
    }

    /// <summary>The symbols this binds, each of which a runtime must export.</summary>
    public static IReadOnlyList<string> Entrypoints { get; } =
    [
        OperateAbi.RemoteOpenEntrypoint,
        OperateAbi.RemotePublicationEntrypoint,
        OperateAbi.RemoteWaitEntrypoint,
        OperateAbi.RemoteActEntrypoint,
        OperateAbi.RemoteEndEntrypoint,
        OperateAbi.RemoteFreeEntrypoint,
        OperateAbi.OperateServeEntrypoint,
        OperateAbi.OperateServedAddressEntrypoint,
        OperateAbi.OperatePostEntrypoint,
        OperateAbi.OperateServeStopEntrypoint,
    ];

    // The calls a remote node and a served listener make on their handles.
    internal readonly delegate* unmanaged[Cdecl]<nint, byte*, nuint, nuint*, ulong*, int>
        Publication;

    internal readonly delegate* unmanaged[Cdecl]<
        nint, ulong, uint, ulong*, ulong*, byte*, byte*, nuint, nuint*, int> Wait;

    internal readonly delegate* unmanaged[Cdecl]<
        nint, XmipStr, XmipStr, XmipStr, XmipStr, byte*, nuint, nuint*, int> Act;

    internal readonly delegate* unmanaged[Cdecl]<nint, void> End;

    internal readonly delegate* unmanaged[Cdecl]<nint, void> Free;

    internal readonly delegate* unmanaged[Cdecl]<nint, byte*, nuint, nuint*, int> ServedAddress;

    internal readonly delegate* unmanaged[Cdecl]<nint, XmipStr, int> Post;

    internal readonly delegate* unmanaged[Cdecl]<nint, void> ServeStop;

    /// <summary>
    /// The node at <paramref name="address"/>, <c>host:port</c>, presenting
    /// <paramref name="certificate"/> with <paramref name="key"/> and trusting
    /// <paramref name="anchors"/>, each PEM text; null with the runtime's
    /// refusal in <paramref name="reason"/>. Nothing is connected yet.
    /// </summary>
    public RemoteNode? Open(
        string address, string certificate, string key, string anchors, out string reason)
    {
        ArgumentNullException.ThrowIfNull(address);
        ArgumentNullException.ThrowIfNull(certificate);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(anchors);

        Utf8Pack pack = new([address, certificate, key, anchors]);
        byte[] report = new byte[Room];
        nuint length = 0;
        nint handle = 0;
        int status;

        fixed (byte* data = pack.Bytes)
        fixed (byte* written = report)
        {
            status = _open(
                pack.Borrow(data, 0), pack.Borrow(data, 1), pack.Borrow(data, 2),
                pack.Borrow(data, 3), &handle, written, (nuint)report.Length, &length);
        }

        reason = status == 0 ? string.Empty : Said(report, length);
        return status == 0 && handle != 0 ? new RemoteNode(this, handle) : null;
    }

    /// <summary>
    /// Serve what is posted to it (<see cref="ServedNode.Post"/>) and the acts
    /// on the nodes of this process on an operate listener at
    /// <paramref name="listen"/>, presenting
    /// <paramref name="certificate"/> with <paramref name="key"/>, taking a
    /// surface whose certificate reaches <paramref name="anchors"/>, every
    /// proven caller holding the role <paramref name="role"/> names, audited
    /// as <paramref name="program"/>; null with why in
    /// <paramref name="reason"/>.
    /// </summary>
    public ServedNode? Serve(
        string listen, string certificate, string key, string anchors, string role,
        string program, out string reason)
    {
        Utf8Pack pack = new([listen, certificate, key, anchors, role, program]);
        byte[] report = new byte[Room];
        nuint length = 0;
        nint handle = 0;
        int status;

        fixed (byte* data = pack.Bytes)
        fixed (byte* written = report)
        {
            status = _serve(
                pack.Borrow(data, 0), pack.Borrow(data, 1), pack.Borrow(data, 2),
                pack.Borrow(data, 3), pack.Borrow(data, 4), pack.Borrow(data, 5), &handle,
                written, (nuint)report.Length, &length);
        }

        reason = status == 0 ? string.Empty : Said(report, length);
        return status == 0 && handle != 0 ? new ServedNode(this, handle) : null;
    }

    /// <summary>Text the runtime wrote, as far as it fit.</summary>
    internal static string Said(byte[] text, nuint length)
    {
        return Encoding.UTF8.GetString(text, 0, (int)Math.Min(length, (nuint)text.Length));
    }
}
