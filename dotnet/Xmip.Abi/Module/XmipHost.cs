using System.Runtime.InteropServices;

namespace Xmip.Abi.Module;

/// <summary>
/// What a module may call back into. <c>XmipHost</c>, section 6 of
/// <c>xmip_module.h</c>: deliberately small, a log, the host's answer on
/// cancellation and the Journey a call is in.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct XmipHost
{
    /// <summary><c>abi_version</c>: the version the host speaks.</summary>
    public uint AbiVersion;

    /// <summary><c>ctx</c>, handed back to every callback.</summary>
    public unsafe void* Ctx;

    /// <summary><c>log</c>: a level, a target and a message.</summary>
    public unsafe delegate* unmanaged[Cdecl]<void*, int, XmipStr, XmipStr, void> Log;

    /// <summary><c>cancelled</c>: non-zero asks the module to unwind.</summary>
    public unsafe delegate* unmanaged[Cdecl]<void*, int> Cancelled;

    /// <summary><c>journey_id</c>: the Journey the call in flight serves.</summary>
    public unsafe delegate* unmanaged[Cdecl]<void*, XmipStr> JourneyId;
}
