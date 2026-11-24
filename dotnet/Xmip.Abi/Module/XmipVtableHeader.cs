using System.Runtime.InteropServices;

namespace Xmip.Abi.Module;

/// <summary>
/// What every trait table begins with. <c>XmipVtableHeader</c>, section 8 of
/// <c>xmip_module.h</c>: <c>configure</c> once, before <c>start</c>, with the
/// artifact's TOML fragment; <c>start</c> and <c>stop</c> in that order.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct XmipVtableHeader
{
    /// <summary><c>trait_major</c>.</summary>
    public uint TraitMajor;

    /// <summary><c>trait_minor</c>.</summary>
    public uint TraitMinor;

    /// <summary><c>configure</c>.</summary>
    public unsafe delegate* unmanaged[Cdecl]<void*, XmipStr, int> Configure;

    /// <summary><c>start</c>.</summary>
    public unsafe delegate* unmanaged[Cdecl]<void*, int> Start;

    /// <summary><c>stop</c>.</summary>
    public unsafe delegate* unmanaged[Cdecl]<void*, int> Stop;
}
