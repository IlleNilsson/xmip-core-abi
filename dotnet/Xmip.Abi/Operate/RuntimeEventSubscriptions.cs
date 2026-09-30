using System.Runtime.InteropServices;
using System.Text;
using Xmip.Abi.Module;

namespace Xmip.Abi.Operate;

/// <summary>
/// Section 11 of <c>include/xmip_operate.h</c>, what an operator lists and
/// does, crossed by P/Invoke (ADR-0065, amendment 2026-09-29): the Event
/// subscriptions this process's hub holds, and pause, resume and remove on
/// one of them. The standing, the acts and their audit are
/// <c>xmip-core-event</c>'s; this binds each call once and decides none of
/// it. The same act left for a node a surface reads through its publication
/// is <see cref="RuntimeSubscriptions.Order"/>, one order for both nouns.
/// </summary>
/// <remarks>Pure in the header's sense: one instance serves a whole process
/// from any thread.</remarks>
public sealed unsafe class RuntimeEventSubscriptions
{
    // A sentence, or a file's path, fits in this; a list is asked for again
    // at its true length.
    private const int Room = 16 * 1024;

    private readonly delegate* unmanaged[Cdecl]<XmipStr, byte*, nuint, nuint*, int> _standing;
    private readonly delegate* unmanaged[Cdecl]<
        ulong, XmipStr, XmipStr, byte*, nuint, nuint*, int> _act;

    internal RuntimeEventSubscriptions(nint library)
    {
        _standing = (delegate* unmanaged[Cdecl]<XmipStr, byte*, nuint, nuint*, int>)
            NativeLibrary.GetExport(library, OperateAbi.EventSubscriptionsEntrypoint);
        _act = (delegate* unmanaged[Cdecl]<ulong, XmipStr, XmipStr, byte*, nuint, nuint*, int>)
            NativeLibrary.GetExport(library, OperateAbi.EventSubscriptionActEntrypoint);
    }

    /// <summary>The symbols this binds, each of which a runtime must export;
    /// the publication's list is <see cref="PublicationReader"/>'s.</summary>
    public static IReadOnlyList<string> Entrypoints { get; } =
    [
        OperateAbi.EventSubscriptionsEntrypoint,
        OperateAbi.EventSubscriptionActEntrypoint,
    ];

    /// <summary>Every Event subscription this process's hub holds, each at
    /// <paramref name="node"/>, the scope this process publishes at.</summary>
    public EventSubscriptionList Standing(string node)
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
            ? EventSubscriptionList.Parse(text.AsMemory(0, (int)needed))
            : throw new InvalidOperationException(
                $"the runtime answered the Event subscriptions with {(XmipStatus)status}");
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

        return RuntimeSubscriptions.Answered(status, "an act on an Event subscription");
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
