using System.Runtime.InteropServices;
using System.Text.Json;
using Xmip.Abi.Module;

namespace Xmip.Abi.Operate;

/// <summary>
/// Section 11 of <c>include/xmip_operate.h</c>, crossed by P/Invoke: Events,
/// subscribed from .NET (ADR-0065 clause 2). Which Events a filter matches,
/// whether the subscriber may see them, the queue and the audit are
/// <c>xmip-core-event</c>'s, forwarded by the runtime's library; this binds
/// each call once for every .NET program and decides none of it.
/// </summary>
/// <remarks>
/// The hub is the process's: a subscription here hears what is published in
/// the process that loaded the library — a node started in it, or a
/// <see cref="Publish"/> — and, where that node is in a cluster, what every
/// other node raises (ADR-0065, amendment 2026-10-02). No argument names the
/// subscriber: it is the account this process runs as, resolved through the
/// Parties of the node serving in the process (ADR-0019, amendment
/// 2026-11-11), so where no node serves — and no export lets one serve in
/// this program's process yet — a subscription is refused, and draining and
/// call back are untested (open problem 66). Its Party is then authorized by
/// the policy this program hands the hub (<see cref="AuthorizeBy"/>); no node
/// hands its own yet (open problem 67). Every call may be made from any
/// thread.
/// </remarks>
public sealed partial class RuntimeEvents
{
    // The gate's sentence fits in this; a longer one is cut, as a refusal
    // is one sentence.
    private const int Said = 1024;

    private readonly delegate* unmanaged[Cdecl]<
        XmipStr, XmipStr, XmipEventFilter*, nuint, nint*, byte*, nuint, nuint*, int> _subscribe;
    private readonly delegate* unmanaged[Cdecl]<
        nint, uint, nuint, nint*, XmipEvent**, nuint*, ulong*, int> _next;
    private readonly delegate* unmanaged[Cdecl]<nint, void> _batchFree;
    private readonly delegate* unmanaged[Cdecl]<
        XmipStr, XmipStr, XmipEventFilter*, nuint,
        delegate* unmanaged[Cdecl]<void*, XmipEvent*, void>, void*, nint*, byte*, nuint, nuint*,
        int> _listen;
    private readonly delegate* unmanaged[Cdecl]<nint, void> _unsubscribe;
    private readonly delegate* unmanaged[Cdecl]<XmipEvent*, nuint*, int> _publish;
    private readonly delegate* unmanaged[Cdecl]<
        delegate* unmanaged[Cdecl]<void*, XmipStr, XmipStr, XmipStr, XmipStr, XmipStr, int>,
        void*, int> _authorize;

    // The policy handed to the hub, kept alive while the hub may call it.
    private static readonly GCHandle[] Policy = new GCHandle[1];
    private readonly delegate* unmanaged[Cdecl]<nint, byte*, nuint, nuint*, int> _batchUnheard;
    private readonly delegate* unmanaged[Cdecl]<byte*, nuint, nuint*, int> _unheard;

    internal RuntimeEvents(nint library)
    {
        _subscribe = (delegate* unmanaged[Cdecl]<
            XmipStr, XmipStr, XmipEventFilter*, nuint, nint*, byte*, nuint, nuint*, int>)
            NativeLibrary.GetExport(library, OperateAbi.EventSubscribeEntrypoint);
        _next = (delegate* unmanaged[Cdecl]<
            nint, uint, nuint, nint*, XmipEvent**, nuint*, ulong*, int>)
            NativeLibrary.GetExport(library, OperateAbi.EventNextEntrypoint);
        _batchFree = (delegate* unmanaged[Cdecl]<nint, void>)
            NativeLibrary.GetExport(library, OperateAbi.EventBatchFreeEntrypoint);
        _listen = (delegate* unmanaged[Cdecl]<
            XmipStr, XmipStr, XmipEventFilter*, nuint,
            delegate* unmanaged[Cdecl]<void*, XmipEvent*, void>, void*, nint*, byte*, nuint,
            nuint*, int>)
            NativeLibrary.GetExport(library, OperateAbi.EventListenEntrypoint);
        _unsubscribe = (delegate* unmanaged[Cdecl]<nint, void>)
            NativeLibrary.GetExport(library, OperateAbi.EventUnsubscribeEntrypoint);
        _publish = (delegate* unmanaged[Cdecl]<XmipEvent*, nuint*, int>)
            NativeLibrary.GetExport(library, OperateAbi.EventPublishEntrypoint);
        _authorize = (delegate* unmanaged[Cdecl]<
            delegate* unmanaged[Cdecl]<void*, XmipStr, XmipStr, XmipStr, XmipStr, XmipStr, int>,
            void*, int>)
            NativeLibrary.GetExport(library, OperateAbi.EventAuthorizeEntrypoint);
        _batchUnheard = (delegate* unmanaged[Cdecl]<nint, byte*, nuint, nuint*, int>)
            NativeLibrary.GetExport(library, OperateAbi.EventBatchUnheardEntrypoint);
        _unheard = (delegate* unmanaged[Cdecl]<byte*, nuint, nuint*, int>)
            NativeLibrary.GetExport(library, OperateAbi.EventUnheardEntrypoint);
    }

    /// <summary>Section 11's symbols, which a runtime must export.</summary>
    public static IReadOnlyList<string> Entrypoints { get; } =
    [
        OperateAbi.EventSubscribeEntrypoint,
        OperateAbi.EventNextEntrypoint,
        OperateAbi.EventBatchFreeEntrypoint,
        OperateAbi.EventListenEntrypoint,
        OperateAbi.EventUnsubscribeEntrypoint,
        OperateAbi.EventPublishEntrypoint,
        OperateAbi.EventAuthorizeEntrypoint,
        OperateAbi.EventBatchUnheardEntrypoint,
        OperateAbi.EventUnheardEntrypoint,
    ];

    /// <summary>
    /// Hand this process's hub <paramref name="decide"/> as its policy, the
    /// only one it has while no node hands its own (open problem 67): asked of
    /// each attempt to subscribe,
    /// it answers allow (<see langword="true"/>), deny
    /// (<see langword="false"/>) or no opinion (<see langword="null"/>), and
    /// nothing having an opinion is a refusal. It replaces the policy handed
    /// before; <see langword="null"/> hands none. Called on the subscribing
    /// thread; an exception it throws is no opinion.
    /// </summary>
    public void AuthorizeBy(Func<EventAuthorization, bool?>? decide)
    {
        GCHandle kept = decide is null ? default : GCHandle.Alloc(decide);
        int status;

        unsafe
        {
            status = decide is null
                ? _authorize(null, null)
                : _authorize(&EventCallbacks.Decide, (void*)GCHandle.ToIntPtr(kept));
        }

        lock (Policy)
        {
            if (Policy[0].IsAllocated)
            {
                unsafe
                {
                    Policy[0].Free();
                }
            }

            Policy[0] = kept;
        }

        EventAnswer.Refused(status, "the policy");
    }

    /// <summary>
    /// The members of the cluster this process's hub does not hear now: what
    /// a listening subscription, which has no drain, asks.
    /// </summary>
    public IReadOnlyList<UnheardRecord> Unheard()
    {
        byte[] json;

        unsafe
        {
            json = EventAnswer.Written((into, cap, len) => _unheard(into, cap, len));
        }

        using JsonDocument read = JsonDocument.Parse(json);
        return UnheardRecord.Read(read.RootElement.GetProperty("unheard"));
    }

    /// <summary>
    /// Subscribe this process, as the Party the account it runs as proves to
    /// be, to what <paramref name="filter"/> asks for, through a queue of
    /// <paramref name="capacity"/> Events (0: the runtime's default), drained
    /// with <see cref="EventSubscription.Next"/>. <paramref name="program"/>
    /// and <paramref name="directory"/> say where the subscription, its
    /// deliveries and its refusals are audited, as <see cref="RuntimeAudit"/>'s
    /// do; null or empty lets the capability decide.
    /// </summary>
    /// <exception cref="UnauthorizedAccessException">No node serving in this
    /// process holds its account as a Party, or the authorization gate refused
    /// the Party; the message is the sentence that refused it.</exception>
    /// <exception cref="ArgumentException">The runtime refused what was asked:
    /// a filter Party that is not a UUID, or an outcome it does not
    /// define.</exception>
    public EventSubscription Subscribe(
        string program,
        string? directory,
        EventFilter? filter = null,
        int capacity = 0)
    {
        return Open(program, directory, filter, capacity, null);
    }

    /// <summary>
    /// Subscribe as <see cref="Subscribe"/> does, and have
    /// <paramref name="heard"/> called with each Event, one at a time, on a
    /// thread the runtime starts for this subscription and never on the
    /// publisher's. An exception it throws is caught before the runtime, and
    /// kept as <see cref="EventSubscription.Fault"/>.
    /// </summary>
    /// <exception cref="UnauthorizedAccessException">As
    /// <see cref="Subscribe"/>.</exception>
    /// <exception cref="ArgumentException">As <see cref="Subscribe"/>.</exception>
    public EventSubscription Listen(
        string program,
        string? directory,
        Action<EventRecord> heard,
        EventFilter? filter = null,
        int capacity = 0)
    {
        ArgumentNullException.ThrowIfNull(heard);

        return Open(program, directory, filter, capacity, new EventListening(heard));
    }

    /// <summary>
    /// Hand <paramref name="record"/> to every matching subscription in this
    /// process; how many queues took it. An empty <see cref="EventRecord.Id"/>
    /// is minted and a time of 0 is now. Never waits for a subscriber.
    /// </summary>
    /// <exception cref="ArgumentException">The runtime refused it: an empty
    /// type or scope, an action or outcome it does not define, an odd
    /// diagnostic, or an identifier that is not a UUID.</exception>
    public int Publish(EventRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        List<string> texts =
        [
            record.Id, record.Type, record.Scope, record.Journey, record.Message, record.Stream,
            record.Endpoint, record.Module, record.Artifact, record.Party,
        ];

        foreach ((string name, string value) in record.Diagnostics)
        {
            texts.Add(name);
            texts.Add(value);
        }

        Utf8Pack pack = new(texts);
        XmipStr[] strings = new XmipStr[texts.Count];
        nuint delivered = 0;
        int status;

        fixed (byte* data = pack.Bytes)
        fixed (XmipStr* each = strings)
        {
            // Every string is pack's, pinned until the publish returns.
            unsafe
            {
                for (int i = 0; i < strings.Length; i++)
                {
                    each[i] = pack.Borrow(data, i);
                }

                XmipEvent raised = new()
                {
                    Id = each[0],
                    Type = each[1],
                    TimeUnixNanos = record.TimeUnixNanos,
                    Action = (int)record.Action,
                    Outcome = (int)record.Outcome,
                    Scope = each[2],
                    Journey = each[3],
                    Message = each[4],
                    Stream = each[5],
                    Endpoint = each[6],
                    Module = each[7],
                    Artifact = each[8],
                    Party = each[9],
                    Diagnostics = each + 10,
                    DiagnosticsLen = (nuint)(strings.Length - 10),
                };

                status = _publish(&raised, &delivered);
            }
        }

        EventAnswer.Refused(status, "an Event");
        return checked((int)delivered);
    }
}
