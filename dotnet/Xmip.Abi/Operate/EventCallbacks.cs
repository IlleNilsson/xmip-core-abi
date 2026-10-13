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
internal static unsafe class EventCallbacks
{
    // The runtime asks this on the subscribing thread. Nothing may cross back
    // into it: an exception is no opinion.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    internal static int Decide(
        void* context, XmipStr party, XmipStr mechanism, XmipStr value, XmipStr scope,
        XmipStr type)
    {
        if (GCHandle.FromIntPtr((nint)context).Target
            is not Func<EventAuthorization, bool?> decide)
        {
            return 0;
        }

        try
        {
            bool? said = decide(new EventAuthorization(
                party.Read(), mechanism.Read(), value.Read(), scope.Read(), type.Read()));
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
    internal static EventRecord Copy(XmipEvent* raised)
    {
        Dictionary<string, string> diagnostics = [with(StringComparer.Ordinal)];

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
}
