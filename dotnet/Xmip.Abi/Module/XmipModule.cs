using System.Runtime.InteropServices;

namespace Xmip.Abi.Module;

/// <summary>The module handle. Section 7 of the header.</summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct XmipModule
{
    /// <summary>What the module says it is.</summary>
    public XmipModuleDescriptor Descriptor;

    /// <summary>Module-private. Opaque to the host, passed back to every
    /// function.</summary>
    public void* State;

    /// <summary>The trait table named by the descriptor's module part. The
    /// host selects the type by that name; a mismatch is a load-time
    /// rejection, not a cast.</summary>
    public void* Vtable;

    /// <summary>Detail for the most recent failing call on this instance.
    /// Borrowed, valid until the next call. Empty is legal.</summary>
    public delegate* unmanaged[Cdecl]<void*, XmipStr> LastError;

    /// <summary>The module frees its own state; the host never does, because
    /// no allocator is shared across this boundary.</summary>
    public delegate* unmanaged[Cdecl]<void*, void> Destroy;
}
