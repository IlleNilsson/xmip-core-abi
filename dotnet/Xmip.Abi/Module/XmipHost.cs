using System.Runtime.InteropServices;

namespace Xmip.Abi.Module;

/// <summary>
/// What a module may call back into. Section 6 of the header. Deliberately
/// small — no allocator, no thread pool, no clock, no configuration store; a
/// module brings its own. What it cannot bring is the host's identity for a
/// journey, and the host's answer on cancellation.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct XmipHost
{
    /// <summary>The boundary version the host speaks.</summary>
    public uint AbiVersion;

    /// <summary>Host-private, passed back to every callback.</summary>
    public void* Ctx;

    /// <summary>A log line: level, target, message.</summary>
    public delegate* unmanaged[Cdecl]<void*, int, XmipStr, XmipStr, void> Log;

    /// <summary>Cooperative cancellation. Non-zero means unwind and return
    /// <see cref="XmipStatus.Cancelled"/>.</summary>
    public delegate* unmanaged[Cdecl]<void*, int> Cancelled;

    /// <summary>Correlation for the call in flight. Empty outside a journey.</summary>
    public delegate* unmanaged[Cdecl]<void*, XmipStr> JourneyId;
}
