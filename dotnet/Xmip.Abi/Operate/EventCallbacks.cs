using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Xmip.Abi.Module;

namespace Xmip.Abi.Operate;

/// <summary>
/// What section 11's runtime calls back into .NET on its own threads — the
/// policy asked of each subscriber, and a listening subscription's Event — and
/// an Event copied out of the runtime's borrow, whole. Nothing may cross back
/// into Rust from either callback.
/// </summary>
internal static class EventCallbacks
{
    // The runtime asks this on the subscribing thread. Nothing may cross back
    // into it: an exception is no opinion.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    internal static int Decide(
        void* context, XmipStr party, XmipStr mechanism, XmipStr value, XmipStr scope,
        XmipStr type)
    {
        try
        {
            // The context is the handle RuntimeEvents.AuthorizeBy allocated
            // and keeps, and the five strings are borrowed for this call.
            EventAuthorization asked;
            object? held;

            unsafe
            {
                held = GCHandle.FromIntPtr((nint)context).Target;
                asked = new EventAuthorization(
                    party.Read(), mechanism.Read(), value.Read(), scope.Read(), type.Read());
            }

            if (held is not Func<EventAuthorization, bool?> decide)
            {
                return 0;
            }

            bool? said = decide(asked);
            return said switch
            {
                true => 1,
                false => -1,
                null => 0,
            };
        }
#pragma warning disable CA1031 // Whatever it is, it must not unwind into Rust.
        catch (Exception)
#pragma warning restore CA1031
        {
            return 0;
        }
    }

    // The runtime calls this on its listener thread. Nothing may cross back
    // into it: an exception is kept for the program to read.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    internal static void Heard(void* context, XmipEvent* raised)
    {
        // The context is the handle the subscription keeps, and the Event is
        // borrowed for this call.
        object? held;

        unsafe
        {
            held = GCHandle.FromIntPtr((nint)context).Target;
        }

        if (held is not EventListening listening)
        {
            return;
        }

        try
        {
            unsafe
            {
                listening.Heard(Copy(raised));
            }
        }
#pragma warning disable CA1031 // Whatever it is, it must not unwind into Rust.
        catch (Exception thrown)
#pragma warning restore CA1031
        {
            listening.Fault = thrown;
        }
    }

    // One Event out of the runtime's borrow, whole. Unsafe: the caller
    // promises the borrow is live.
    internal static unsafe EventRecord Copy(XmipEvent* raised)
    {
        Dictionary<string, string> diagnostics = [with(StringComparer.Ordinal)];

        unsafe
        {
            for (nuint at = 0; at + 1 < raised->DiagnosticsLen; at += 2)
            {
                diagnostics[raised->Diagnostics[at].Read()] =
                    raised->Diagnostics[at + 1].Read();
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
    }
}
