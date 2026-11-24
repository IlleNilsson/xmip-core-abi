using System.Runtime.InteropServices;

namespace Xmip.Abi.Module;

/// <summary>
/// Where a Promote unit hands each value it promotes.
/// <c>XmipPromotionSink</c>, section 14 of <c>xmip_module.h</c>: the
/// property, the value's kind and its bytes, borrowed for that call.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct XmipPromotionSink
{
    /// <summary><c>ctx</c>, handed back to <see cref="Promoted"/>.</summary>
    public unsafe void* Ctx;

    /// <summary><c>promoted</c>.</summary>
    public unsafe delegate* unmanaged[Cdecl]<
        void*, XmipStr, XmipValueKind, XmipSlice, int> Promoted;
}
