using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
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
/// <see cref="Publish"/> — and nothing another process publishes. Every
/// call may be made from any thread.
/// </remarks>
public sealed unsafe class RuntimeEvents
{
    // The gate's sentence fits in this; a longer one is cut, as a refusal
    // is one sentence.
    private const int Said = 1024;

    private readonly delegate* unmanaged[Cdecl]<
        XmipStr, XmipStr, XmipStr, XmipEventFilter*, nuint, nint*, byte*, nuint, nuint*, int>
        _subscribe;
    private readonly delegate* unmanaged[Cdecl]<
        nint, uint, nuint, nint*, XmipEvent**, nuint*, ulong*, int> _next;
    private readonly delegate* unmanaged[Cdecl]<nint, void> _batchFree;
    private readonly delegate* unmanaged[Cdecl]<
        XmipStr, XmipStr, XmipStr, XmipEventFilter*, nuint,
        delegate* unmanaged[Cdecl]<void*, XmipEvent*, void>, void*, nint*, byte*, nuint, nuint*,
        int> _listen;
    private readonly delegate* unmanaged[Cdecl]<nint, void> _unsubscribe;
    private readonly delegate* unmanaged[Cdecl]<XmipEvent*, nuint*, int> _publish;

    internal RuntimeEvents(nint library)
    {
        _subscribe = (delegate* unmanaged[Cdecl]<
            XmipStr, XmipStr, XmipStr, XmipEventFilter*, nuint, nint*, byte*, nuint, nuint*, int>)
            NativeLibrary.GetExport(library, OperateAbi.EventSubscribeEntrypoint);
        _next = (delegate* unmanaged[Cdecl]<
            nint, uint, nuint, nint*, XmipEvent**, nuint*, ulong*, int>)
            NativeLibrary.GetExport(library, OperateAbi.EventNextEntrypoint);
        _batchFree = (delegate* unmanaged[Cdecl]<nint, void>)
            NativeLibrary.GetExport(library, OperateAbi.EventBatchFreeEntrypoint);
        _listen = (delegate* unmanaged[Cdecl]<
            XmipStr, XmipStr, XmipStr, XmipEventFilter*, nuint,
            delegate* unmanaged[Cdecl]<void*, XmipEvent*, void>, void*, nint*, byte*, nuint,
            nuint*, int>)
            NativeLibrary.GetExport(library, OperateAbi.EventListenEntrypoint);
        _unsubscribe = (delegate* unmanaged[Cdecl]<nint, void>)
            NativeLibrary.GetExport(library, OperateAbi.EventUnsubscribeEntrypoint);
        _publish = (delegate* unmanaged[Cdecl]<XmipEvent*, nuint*, int>)
            NativeLibrary.GetExport(library, OperateAbi.EventPublishEntrypoint);
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
    ];

    /// <summary>
    /// Subscribe <paramref name="subscriber"/>, a Party's UUID, to what
    /// <paramref name="filter"/> asks for, through a queue of
    /// <paramref name="capacity"/> Events (0: the runtime's default), drained
    /// with <see cref="EventSubscription.Next"/>. <paramref name="program"/>
    /// and <paramref name="directory"/> say where the subscription, its
    /// deliveries and its refusals are audited, as <see cref="RuntimeAudit"/>'s
    /// do; null or empty lets the capability decide.
    /// </summary>
    /// <exception cref="UnauthorizedAccessException">The authorization gate
    /// refused it; the message is the gate's sentence.</exception>
    /// <exception cref="ArgumentException">The runtime refused what was asked:
    /// no subscriber, one that is not a UUID, or an outcome it does not
    /// define.</exception>
    public EventSubscription Subscribe(
        string program,
        string? directory,
        string subscriber,
        EventFilter? filter = null,
        int capacity = 0)
    {
        return Open(program, directory, subscriber, filter, capacity, null);
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
        string subscriber,
        Action<EventRecord> heard,
        EventFilter? filter = null,
        int capacity = 0)
    {
        ArgumentNullException.ThrowIfNull(heard);

        return Open(program, directory, subscriber, filter, capacity, new EventListening(heard));
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

        Refused(status, "an Event");
        return checked((int)delivered);
    }

    internal EventDelivery Next(EventSubscription subscription, TimeSpan timeout, int max)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(timeout, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(max, 1);

        uint milliseconds = (uint)Math.Min(Math.Ceiling(timeout.TotalMilliseconds), uint.MaxValue);
        bool held = false;
        nint batch = 0;
        XmipEvent* events = null;
        nuint count = 0;
        ulong refused = 0;
        int status;

        subscription.DangerousAddRef(ref held);

        try
        {
            status = _next(subscription.DangerousGetHandle(), milliseconds, (nuint)max, &batch,
                &events, &count, &refused);
        }
        finally
        {
            if (held)
            {
                subscription.DangerousRelease();
            }
        }

        if (status == (int)XmipStatus.Timeout)
        {
            return EventDelivery.Nothing;
        }

        if (status == (int)XmipStatus.State)
        {
            throw new InvalidOperationException("A listening subscription is never drained.");
        }

        try
        {
            Refused(status, "a drain");

            EventRecord[] copied = new EventRecord[checked((int)count)];

            for (int at = 0; at < copied.Length; at++)
            {
                copied[at] = Copy(events + at);
            }

            return new EventDelivery(copied, refused);
        }
        finally
        {
            _batchFree(batch);
        }
    }

    internal void Unsubscribe(nint subscription)
    {
        _unsubscribe(subscription);
    }

    private EventSubscription Open(
        string program,
        string? directory,
        string subscriber,
        EventFilter? filter,
        int capacity,
        EventListening? listening)
    {
        ArgumentNullException.ThrowIfNull(program);
        ArgumentNullException.ThrowIfNull(subscriber);
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);

        filter ??= EventFilter.Everything;

        // program, directory, subscriber, scope, party, then the types.
        List<string> texts =
            [program, directory ?? string.Empty, subscriber, filter.Scope, filter.Party];
        texts.AddRange(filter.Types);

        Utf8Pack pack = new(texts);
        XmipStr[] strings = new XmipStr[texts.Count];
        int[] outcomes = [.. filter.Outcomes.Select(outcome => (int)outcome)];
        byte[] said = new byte[Said];
        nuint saidLength = 0;
        nint handle = 0;
        GCHandle context = listening is null ? default : GCHandle.Alloc(listening);
        int status;

        fixed (byte* data = pack.Bytes)
        fixed (XmipStr* each = strings)
        fixed (int* wanted = outcomes)
        fixed (byte* sentence = said)
        {
            for (int i = 0; i < strings.Length; i++)
            {
                each[i] = pack.Borrow(data, i);
            }

            XmipEventFilter asked = new()
            {
                Types = each + 5,
                TypesLen = (nuint)(strings.Length - 5),
                Outcomes = wanted,
                OutcomesLen = (nuint)outcomes.Length,
                Scope = each[3],
                Party = each[4],
            };

            status = listening is null
                ? _subscribe(each[0], each[1], each[2], &asked, (nuint)capacity, &handle,
                    sentence, (nuint)said.Length, &saidLength)
                : _listen(each[0], each[1], each[2], &asked, (nuint)capacity, &Heard,
                    (void*)GCHandle.ToIntPtr(context), &handle, sentence, (nuint)said.Length,
                    &saidLength);
        }

        if (status != 0)
        {
            if (context.IsAllocated)
            {
                context.Free();
            }

            if (status == (int)XmipStatus.Auth)
            {
                throw new UnauthorizedAccessException(Encoding.UTF8.GetString(
                    said, 0, (int)Math.Min(saidLength, (nuint)said.Length)));
            }

            Refused(status, "the subscription");
        }

        EventSubscription subscription = new(this, listening);

        subscription.Adopt(handle, context);
        return subscription;
    }

    // The runtime calls this on its listener thread. Nothing may cross back
    // into it: an exception is kept for the program to read.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void Heard(void* context, XmipEvent* raised)
    {
        if (GCHandle.FromIntPtr((nint)context).Target is not EventListening listening)
        {
            return;
        }

        try
        {
            listening.Heard(Copy(raised));
        }
#pragma warning disable CA1031 // Whatever it is, it must not unwind into Rust.
        catch (Exception thrown)
#pragma warning restore CA1031
        {
            listening.Fault = thrown;
        }
    }

    // One Event out of the runtime's borrow, whole.
    private static EventRecord Copy(XmipEvent* raised)
    {
        Dictionary<string, string> diagnostics = new(StringComparer.Ordinal);

        for (nuint at = 0; at + 1 < raised->DiagnosticsLen; at += 2)
        {
            diagnostics[raised->Diagnostics[at].Read()] = raised->Diagnostics[at + 1].Read();
        }

        return new EventRecord
        {
            Id = raised->Id.Read(),
            Type = raised->Type.Read(),
            TimeUnixNanos = raised->TimeUnixNanos,
            Action = (EventAction)raised->Action,
            Outcome = (EventOutcome)raised->Outcome,
            Scope = raised->Scope.Read(),
            Journey = raised->Journey.Read(),
            Message = raised->Message.Read(),
            Stream = raised->Stream.Read(),
            Endpoint = raised->Endpoint.Read(),
            Module = raised->Module.Read(),
            Artifact = raised->Artifact.Read(),
            Party = raised->Party.Read(),
            Diagnostics = diagnostics,
        };
    }

    // What the runtime refused, in its status's words; nothing on XMIP_OK.
    private static void Refused(int status, string what)
    {
        if (status == 0)
        {
            return;
        }

        XmipStatus said = (XmipStatus)status;

        throw said is XmipStatus.Invalid or XmipStatus.Malformed
            ? new ArgumentException($"The runtime refused {what}: {said.Explain()}")
            : new InvalidOperationException($"The runtime answered {what} with {said}: " +
                said.Explain());
    }
}
