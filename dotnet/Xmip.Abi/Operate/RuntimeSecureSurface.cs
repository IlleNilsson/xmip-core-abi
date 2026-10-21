using System.Runtime.InteropServices;
using System.Text.Json;
using Xmip.Abi.Module;

namespace Xmip.Abi.Operate;

/// <summary>
/// Section 21 of <c>include/xmip_operate.h</c>, crossed by P/Invoke: a web
/// host's secure surface, protected by Xmip itself (ADR-0009, amendment
/// 2026-10-21). A host hands the protection of its connections to the
/// runtime and keeps HTTP: <see cref="Open"/> gives a
/// <see cref="SecureSurfaceListener"/> whose sessions a web server reads and
/// writes, the <see cref="Protection"/> a parameter, and <see cref="Issue"/>
/// issues the host a certificate under an authority kept in a folder. The
/// handshake, the anchors' verdict and every word are <c>xmip-core-library-tls</c>'s and
/// <c>xmip-core-provision</c>'s; this binds each call once.
/// </summary>
/// <remarks>A node's build only, as sections 10 to 16.</remarks>
public sealed unsafe class RuntimeSecureSurface
{
    private readonly delegate* unmanaged[Cdecl]<
        XmipStr, XmipStr, XmipStr, XmipStr, byte*, nuint, nuint*, int> _issue;

    private readonly delegate* unmanaged[Cdecl]<
        int, XmipStr, XmipStr, XmipStr, XmipStr, nint*, byte*, nuint, nuint*, int> _open;

    internal RuntimeSecureSurface(nint library)
    {
        _issue = (delegate* unmanaged[Cdecl]<
            XmipStr, XmipStr, XmipStr, XmipStr, byte*, nuint, nuint*, int>)
            NativeLibrary.GetExport(library, OperateAbi.HostIssueEntrypoint);
        _open = (delegate* unmanaged[Cdecl]<
            int, XmipStr, XmipStr, XmipStr, XmipStr, nint*, byte*, nuint, nuint*, int>)
            NativeLibrary.GetExport(library, OperateAbi.ListenerOpenEntrypoint);
        Address = (delegate* unmanaged[Cdecl]<nint, byte*, nuint, nuint*, int>)
            NativeLibrary.GetExport(library, OperateAbi.ListenerAddressEntrypoint);
        Accept = (delegate* unmanaged[Cdecl]<nint, nint*, byte*, nuint, nuint*, int>)
            NativeLibrary.GetExport(library, OperateAbi.ListenerAcceptEntrypoint);
        Stop = (delegate* unmanaged[Cdecl]<nint, void>)
            NativeLibrary.GetExport(library, OperateAbi.ListenerStopEntrypoint);
        Free = (delegate* unmanaged[Cdecl]<nint, void>)
            NativeLibrary.GetExport(library, OperateAbi.ListenerFreeEntrypoint);
        Peer = (delegate* unmanaged[Cdecl]<nint, byte*, nuint, nuint*, int>)
            NativeLibrary.GetExport(library, OperateAbi.SessionPeerEntrypoint);
        Ends = (delegate* unmanaged[Cdecl]<nint, byte*, nuint, nuint*, int>)
            NativeLibrary.GetExport(library, OperateAbi.SessionEndsEntrypoint);
        Read = (delegate* unmanaged[Cdecl]<nint, byte*, nuint, nuint*, int>)
            NativeLibrary.GetExport(library, OperateAbi.SessionReadEntrypoint);
        Write = (delegate* unmanaged[Cdecl]<nint, XmipStr, int>)
            NativeLibrary.GetExport(library, OperateAbi.SessionWriteEntrypoint);
        Close = (delegate* unmanaged[Cdecl]<nint, void>)
            NativeLibrary.GetExport(library, OperateAbi.SessionCloseEntrypoint);
        SessionFree = (delegate* unmanaged[Cdecl]<nint, void>)
            NativeLibrary.GetExport(library, OperateAbi.SessionFreeEntrypoint);
    }

    /// <summary>Section 21's symbols, which a node's runtime exports.</summary>
    public static IReadOnlyList<string> Entrypoints { get; } =
    [
        OperateAbi.HostIssueEntrypoint,
        OperateAbi.ListenerOpenEntrypoint,
        OperateAbi.ListenerAddressEntrypoint,
        OperateAbi.ListenerAcceptEntrypoint,
        OperateAbi.ListenerStopEntrypoint,
        OperateAbi.ListenerFreeEntrypoint,
        OperateAbi.SessionPeerEntrypoint,
        OperateAbi.SessionEndsEntrypoint,
        OperateAbi.SessionReadEntrypoint,
        OperateAbi.SessionWriteEntrypoint,
        OperateAbi.SessionCloseEntrypoint,
        OperateAbi.SessionFreeEntrypoint,
    ];

    // The calls a listener and a session make on their handles.
    internal readonly delegate* unmanaged[Cdecl]<nint, byte*, nuint, nuint*, int> Address;

    internal readonly delegate* unmanaged[Cdecl]<nint, nint*, byte*, nuint, nuint*, int> Accept;

    internal readonly delegate* unmanaged[Cdecl]<nint, void> Stop;

    internal readonly delegate* unmanaged[Cdecl]<nint, void> Free;

    internal readonly delegate* unmanaged[Cdecl]<nint, byte*, nuint, nuint*, int> Peer;

    internal readonly delegate* unmanaged[Cdecl]<nint, byte*, nuint, nuint*, int> Ends;

    internal readonly delegate* unmanaged[Cdecl]<nint, byte*, nuint, nuint*, int> Read;

    internal readonly delegate* unmanaged[Cdecl]<nint, XmipStr, int> Write;

    internal readonly delegate* unmanaged[Cdecl]<nint, void> Close;

    internal readonly delegate* unmanaged[Cdecl]<nint, void> SessionFree;

    /// <summary>
    /// The authority kept in the folder <paramref name="authority"/> — made
    /// there, called <paramref name="name"/>, where none is kept — and a
    /// fresh key and certificate for <paramref name="host"/> under it,
    /// written by the runtime into <paramref name="folder"/> as private files,
    /// the key never entering this process: where the chain, the key and
    /// the authority's certificate, the anchor, are. Or false with the
    /// runtime's sentence in <paramref name="refused"/>.
    /// </summary>
    public bool Issue(
        string authority, string name, string host, string folder,
        out SecureSurfaceIssued issued, out string refused)
    {
        ArgumentNullException.ThrowIfNull(authority);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(folder);

        Utf8Pack pack = new([authority, name, host, folder]);
        byte[] room = new byte[RuntimeRemotes.Room];
        nuint length = 0;
        int status;

        fixed (byte* data = pack.Bytes)
        fixed (byte* written = room)
        {
            status = _issue(
                pack.Borrow(data, 0), pack.Borrow(data, 1), pack.Borrow(data, 2),
                pack.Borrow(data, 3), written, (nuint)room.Length, &length);
        }

        string text = RuntimeRemotes.Said(room, length);
        issued = new SecureSurfaceIssued(string.Empty, string.Empty, string.Empty);
        refused = string.Empty;

        if (status != 0)
        {
            refused = status == (int)XmipStatus.Invalid
                ? text
                : $"the runtime answered with {(XmipStatus)status}";
            return false;
        }

        using JsonDocument answer = JsonDocument.Parse(text);
        JsonElement root = answer.RootElement;
        issued = new SecureSurfaceIssued(
            root.GetProperty("certificate").GetString() ?? string.Empty,
            root.GetProperty("key").GetString() ?? string.Empty,
            root.GetProperty("anchor").GetString() ?? string.Empty);
        return true;
    }

    /// <summary>
    /// A listener on <paramref name="listen"/>, <c>host:port</c>, protected by
    /// <paramref name="protection"/> (<see cref="Protection.Tls"/>, the one
    /// built), presenting
    /// <paramref name="certificate"/> with <paramref name="key"/> and
    /// verifying a browser's certificate against <paramref name="anchors"/>,
    /// each PEM text; a browser that presents none is let through as no one.
    /// Null with the runtime's reason.
    /// </summary>
    public SecureSurfaceListener? Open(
        Protection protection,
        string listen, string certificate, string key, string anchors, out string reason)
    {
        ArgumentNullException.ThrowIfNull(listen);
        ArgumentNullException.ThrowIfNull(certificate);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(anchors);

        Utf8Pack pack = new([listen, certificate, key, anchors]);
        byte[] report = new byte[RuntimeRemotes.Room];
        nuint length = 0;
        nint handle = 0;
        int status;

        fixed (byte* data = pack.Bytes)
        fixed (byte* written = report)
        {
            status = _open(
                (int)protection,
                pack.Borrow(data, 0), pack.Borrow(data, 1), pack.Borrow(data, 2),
                pack.Borrow(data, 3), &handle, written, (nuint)report.Length, &length);
        }

        Array.Clear(pack.Bytes);
        reason = status == 0 ? string.Empty : RuntimeRemotes.Said(report, length);
        return status == 0 && handle != 0 ? new SecureSurfaceListener(this, handle) : null;
    }
}
