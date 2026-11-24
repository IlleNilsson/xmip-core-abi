using System.Runtime.InteropServices;

namespace Xmip.Abi.Module;

/// <summary>
/// A Promote unit. <c>XmipPromoteVtable</c>, section 14 of
/// <c>xmip_module.h</c>: <c>open</c> a step entry once, with its stage word
/// and its settings as one JSON object; <c>promote</c> a Message's content
/// into the sink, from any thread; <c>release</c> the step.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct XmipPromoteVtable
{
    /// <summary><c>header</c>.</summary>
    public XmipVtableHeader Header;

    /// <summary><c>open</c>.</summary>
    public unsafe delegate* unmanaged[Cdecl]<void*, XmipStr, XmipStr, void**, int> Open;

    /// <summary><c>release</c>.</summary>
    public unsafe delegate* unmanaged[Cdecl]<void*, void*, void> Release;

    /// <summary><c>promote</c>.</summary>
    public unsafe delegate* unmanaged[Cdecl]<
        void*, void*, XmipReader*, XmipPromotionSink*, int> Promote;
}
