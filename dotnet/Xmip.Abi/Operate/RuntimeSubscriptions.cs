using System.Runtime.InteropServices;
using System.Text;
using Xmip.Abi.Module;

namespace Xmip.Abi.Operate;

/// <summary>
/// Section 14 of <c>include/xmip_operate.h</c>, crossed by P/Invoke
/// (ADR-0013, amendment 2026-09-30): the Subscriptions of every node running
/// in this process, pause and resume on one of them, and an operator's order
/// — on a Subscription or an Event subscription — left for a node a surface
/// reads through its publication. The standing, the hold, the acts, their
/// words and audit, and the order's file are the runtime's and
/// <c>observe</c>'s; this binds each call once and decides none of it. There
/// is no remove: a Subscription is added and removed in the TOML
/// configuration.
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
        XmipStr, XmipStr, XmipStr, XmipStr, byte*, nuint, nuint*, int> _act;
    private readonly delegate* unmanaged[Cdecl]<
        XmipStr, XmipStr, XmipStr, XmipStr, XmipStr, XmipStr, byte*, nuint, nuint*, int> _order;

    internal RuntimeSubscriptions(nint library)
    {
        _standing = (delegate* unmanaged[Cdecl]<XmipStr, byte*, nuint, nuint*, int>)
            NativeLibrary.GetExport(library, OperateAbi.SubscriptionsEntrypoint);
        _act = (delegate* unmanaged[Cdecl]<
            XmipStr, XmipStr, XmipStr, XmipStr, byte*, nuint, nuint*, int>)
            NativeLibrary.GetExport(library, OperateAbi.SubscriptionActEntrypoint);
        _order = (delegate* unmanaged[Cdecl]<
            XmipStr, XmipStr, XmipStr, XmipStr, XmipStr, XmipStr, byte*, nuint, nuint*, int>)
            NativeLibrary.GetExport(library, OperateAbi.OrderEntrypoint);
    }

    /// <summary>The symbols this binds, each of which a runtime must export;
    /// the publication's list is <see cref="PublicationReader"/>'s.</summary>
    public static IReadOnlyList<string> Entrypoints { get; } =
    [
        OperateAbi.SubscriptionsEntrypoint,
        OperateAbi.SubscriptionActEntrypoint,
        OperateAbi.OrderEntrypoint,
    ];

    /// <summary>Every Subscription of every node running in this process at
    /// or beneath <paramref name="node"/>; empty for every one.</summary>
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
                $"the runtime answered the Subscriptions with {(XmipStatus)status}");
    }

    /// <summary>
    /// Apply <paramref name="act"/> — pause or resume — to the Subscription
    /// <paramref name="name"/> of the node at <paramref name="node"/> in this
    /// process, by <paramref name="who"/>. <see cref="XmipStatus.Ok"/> with
    /// what came of it in <paramref name="said"/>;
    /// <see cref="XmipStatus.NotFound"/> or <see cref="XmipStatus.Invalid"/>
    /// with the refusal — remove among them.
    /// </summary>
    public XmipStatus Act(string node, string name, string act, string who, out string said)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(act);
        ArgumentNullException.ThrowIfNull(who);

        Utf8Pack pack = new([node, name, act, who]);
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

        return Answered(status, "an act on a Subscription");
    }

    /// <summary>
    /// Leave <paramref name="act"/> on the <paramref name="noun"/> —
    /// <c>subscription</c> or <c>event-subscription</c> — called
    /// <paramref name="target"/> of the node at <paramref name="node"/> in
    /// <paramref name="orders"/>, the place its publication names, for the
    /// node to take at its next look. <see cref="XmipStatus.Ok"/> with the
    /// file written in <paramref name="said"/>; <see cref="XmipStatus.Invalid"/>
    /// or <see cref="XmipStatus.Io"/> with why not.
    /// </summary>
    public XmipStatus Order(
        string orders, string node, string noun, string target, string act, string who,
        out string said)
    {
        ArgumentNullException.ThrowIfNull(orders);
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(noun);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(act);
        ArgumentNullException.ThrowIfNull(who);

        Utf8Pack pack = new([orders, node, noun, target, act, who]);
        byte[] text = new byte[Room];
        nuint length = 0;
        int status;

        fixed (byte* data = pack.Bytes)
        fixed (byte* written = text)
        {
            status = _order(
                pack.Borrow(data, 0), pack.Borrow(data, 1), pack.Borrow(data, 2),
                pack.Borrow(data, 3), pack.Borrow(data, 4), pack.Borrow(data, 5), written,
                (nuint)text.Length, &length);
        }

        said = Encoding.UTF8.GetString(text, 0, (int)Math.Min(length, (nuint)text.Length));

        return Answered(status, "an operator's order");
    }

    /// <summary>What an act or an order answered: a status a caller reads,
    /// or the fault any other is.</summary>
    internal static XmipStatus Answered(int status, string what)
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
