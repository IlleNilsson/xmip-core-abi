using System.Runtime.InteropServices;
using System.Text;
using Xmip.Abi.Module;

namespace Xmip.Abi.Operate;

/// <summary>
/// Section 11 of <c>include/xmip_operate.h</c>, what an operator lists and
/// does, crossed by P/Invoke (ADR-0065, amendment 2026-09-29): the
/// subscriptions this process's hub holds, pause, resume and remove on one of
/// them, and the same act left for a node a surface reads through its
/// publication. The standing, the acts, their words, their audit and the
/// order's file are <c>xmip-core-event</c>'s; this binds each call once and
/// decides none of it.
/// </summary>
/// <remarks>Pure in the header's sense: one instance serves a whole process
/// from any thread.</remarks>
public sealed unsafe class RuntimeSubscriptions
{
    // A sentence, or a file's path, fits in this; a list is asked for again
    // at its true length.
    private const int Room = 16 * 1024;

    private readonly delegate* unmanaged[Cdecl]<XmipStr, byte*, nuint, nuint*, int> _standing;
    private readonly delegate* unmanaged[Cdecl]<
        ulong, XmipStr, XmipStr, byte*, nuint, nuint*, int> _act;
    private readonly delegate* unmanaged[Cdecl]<
        XmipStr, XmipStr, ulong, XmipStr, XmipStr, byte*, nuint, nuint*, int> _order;

    internal RuntimeSubscriptions(nint library)
    {
        _standing = (delegate* unmanaged[Cdecl]<XmipStr, byte*, nuint, nuint*, int>)
            NativeLibrary.GetExport(library, OperateAbi.EventSubscriptionsEntrypoint);
        _act = (delegate* unmanaged[Cdecl]<ulong, XmipStr, XmipStr, byte*, nuint, nuint*, int>)
            NativeLibrary.GetExport(library, OperateAbi.EventSubscriptionActEntrypoint);
        _order = (delegate* unmanaged[Cdecl]<
            XmipStr, XmipStr, ulong, XmipStr, XmipStr, byte*, nuint, nuint*, int>)
            NativeLibrary.GetExport(library, OperateAbi.EventSubscriptionOrderEntrypoint);
    }

    /// <summary>The symbols this binds, each of which a runtime must export;
    /// the publication's list is <see cref="PublicationReader"/>'s.</summary>
    public static IReadOnlyList<string> Entrypoints { get; } =
    [
        OperateAbi.EventSubscriptionsEntrypoint,
        OperateAbi.EventSubscriptionActEntrypoint,
        OperateAbi.EventSubscriptionOrderEntrypoint,
    ];

    /// <summary>Every subscription this process's hub holds, each at
    /// <paramref name="node"/>, the scope this process publishes at.</summary>
    public SubscriptionList Standing(string node)
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
            ? SubscriptionList.Parse(text.AsMemory(0, (int)needed))
            : throw new InvalidOperationException(
                $"the runtime answered the subscriptions with {(XmipStatus)status}");
    }

    /// <summary>
    /// Apply <paramref name="act"/> — pause, resume or remove — to
    /// subscription <paramref name="id"/> in this process's hub, by
    /// <paramref name="who"/>. <see cref="XmipStatus.Ok"/> with what came of
    /// it in <paramref name="said"/>; <see cref="XmipStatus.NotFound"/> or
    /// <see cref="XmipStatus.Invalid"/> with the refusal.
    /// </summary>
    public XmipStatus Act(ulong id, string act, string who, out string said)
    {
        ArgumentNullException.ThrowIfNull(act);
        ArgumentNullException.ThrowIfNull(who);

        Utf8Pack pack = new([act, who]);
        byte[] text = new byte[Room];
        nuint length = 0;
        int status;

        fixed (byte* data = pack.Bytes)
        fixed (byte* written = text)
        {
            status = _act(
                id, pack.Borrow(data, 0), pack.Borrow(data, 1), written, (nuint)text.Length,
                &length);
        }

        said = Encoding.UTF8.GetString(text, 0, (int)Math.Min(length, (nuint)text.Length));

        return Answered(status, "an act on a subscription");
    }

    /// <summary>
    /// Leave <paramref name="act"/> on subscription <paramref name="id"/> of
    /// the node at <paramref name="node"/> in <paramref name="orders"/>, the
    /// place its publication names, for the node to take at its next look.
    /// <see cref="XmipStatus.Ok"/> with the file written in
    /// <paramref name="said"/>; <see cref="XmipStatus.Invalid"/> or
    /// <see cref="XmipStatus.Io"/> with why not.
    /// </summary>
    public XmipStatus Order(
        string orders, string node, ulong id, string act, string who, out string said)
    {
        ArgumentNullException.ThrowIfNull(orders);
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(act);
        ArgumentNullException.ThrowIfNull(who);

        Utf8Pack pack = new([orders, node, act, who]);
        byte[] text = new byte[Room];
        nuint length = 0;
        int status;

        fixed (byte* data = pack.Bytes)
        fixed (byte* written = text)
        {
            status = _order(
                pack.Borrow(data, 0), pack.Borrow(data, 1), id, pack.Borrow(data, 2),
                pack.Borrow(data, 3), written, (nuint)text.Length, &length);
        }

        said = Encoding.UTF8.GetString(text, 0, (int)Math.Min(length, (nuint)text.Length));

        return Answered(status, "an order on a subscription");
    }

    private static XmipStatus Answered(int status, string what)
    {
        return status switch
        {
            0 or (int)XmipStatus.Invalid or (int)XmipStatus.NotFound or (int)XmipStatus.Io
                => (XmipStatus)status,
            _ => throw new InvalidOperationException(
                $"the runtime answered {what} with {(XmipStatus)status}"),
        };
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
