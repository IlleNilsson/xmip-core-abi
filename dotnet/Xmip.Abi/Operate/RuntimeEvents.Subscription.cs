using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Xmip.Abi.Module;

namespace Xmip.Abi.Operate;

// Section 11's subscription drained, listened to and ended: the calls an
// EventSubscription makes on its handle.
public sealed partial class RuntimeEvents
{
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
            unsafe
            {
                status = _next(subscription.DangerousGetHandle(), milliseconds, (nuint)max, &batch,
                    &events, &count, &refused);
            }
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
            EventAnswer.Refused(status, "a drain");

            EventRecord[] copied = new EventRecord[checked((int)count)];

            for (int at = 0; at < copied.Length; at++)
            {
                unsafe
                {
                    copied[at] = EventCallbacks.Copy(events + at);
                }
            }

            nint drained = batch;
            byte[] json;

            unsafe
            {
                json = EventAnswer.Written(
                    (into, cap, len) => _batchUnheard(drained, into, cap, len));
            }

            using JsonDocument read = JsonDocument.Parse(json);
            JsonElement root = read.RootElement;

            return new EventDelivery(
                copied,
                refused,
                UnheardRecord.Read(root.GetProperty("unheard")),
                root.GetProperty("changed").GetBoolean());
        }
        finally
        {
            unsafe
            {
                _batchFree(batch);
            }
        }
    }

    internal void Unsubscribe(nint subscription)
    {
        unsafe
        {
            _unsubscribe(subscription);
        }
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
            // Every string is pack's and the outcomes this frame's, pinned
            // until the call returns.
            unsafe
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
                    : _listen(each[0], each[1], each[2], &asked, (nuint)capacity,
                        &EventCallbacks.Heard, (void*)GCHandle.ToIntPtr(context), &handle,
                        sentence, (nuint)said.Length, &saidLength);
            }
        }

        if (status != 0)
        {
            if (context.IsAllocated)
            {
                unsafe
                {
                    context.Free();
                }
            }

            if (status == (int)XmipStatus.Auth)
            {
                throw new UnauthorizedAccessException(Encoding.UTF8.GetString(
                    said, 0, (int)Math.Min(saidLength, (nuint)said.Length)));
            }

            EventAnswer.Refused(status, "the subscription");
        }

        EventSubscription subscription = new(this, listening);

        subscription.Adopt(handle, context);
        return subscription;
    }
}
