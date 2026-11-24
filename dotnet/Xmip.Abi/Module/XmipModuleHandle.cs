using System.Runtime.InteropServices;

namespace Xmip.Abi.Module;

/// <summary>
/// The module handle the entrypoint fills. <c>XmipModule</c>, section 7 of
/// <c>xmip_module.h</c>; called a handle here, as the header calls it,
/// because <c>Module</c> is the namespace it stands in.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct XmipModuleHandle
{
    /// <summary><c>descriptor</c>.</summary>
    public XmipModuleDescriptor Descriptor;

    /// <summary><c>state</c>: module-private, passed back to every
    /// function.</summary>
    public unsafe void* State;

    /// <summary><c>vtable</c>: the trait table the descriptor's module
    /// names.</summary>
    public unsafe void* Vtable;

    /// <summary><c>last_error</c>: detail for the most recent failing
    /// call.</summary>
    public unsafe delegate* unmanaged[Cdecl]<void*, XmipStr> LastError;

    /// <summary><c>destroy</c>.</summary>
    public unsafe delegate* unmanaged[Cdecl]<void*, void> Destroy;
}
