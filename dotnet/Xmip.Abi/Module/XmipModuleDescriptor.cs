using System.Runtime.InteropServices;

namespace Xmip.Abi.Module;

/// <summary>
/// What a module says it is. <c>XmipModuleDescriptor</c>, section 4 of
/// <c>xmip_module.h</c>: the three name parts of ADR-0011, the trait version
/// it was built against and its own version.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct XmipModuleDescriptor
{
    /// <summary><c>abi_version</c>.</summary>
    public uint AbiVersion;

    /// <summary><c>provider</c>.</summary>
    public XmipStr Provider;

    /// <summary><c>module</c>: the trait its table is.</summary>
    public XmipStr Module;

    /// <summary><c>standard</c>; empty only when the provider is core.</summary>
    public XmipStr Standard;

    /// <summary><c>trait_major</c>.</summary>
    public uint TraitMajor;

    /// <summary><c>trait_minor</c>.</summary>
    public uint TraitMinor;

    /// <summary><c>module_major</c>.</summary>
    public uint ModuleMajor;

    /// <summary><c>module_minor</c>.</summary>
    public uint ModuleMinor;

    /// <summary><c>module_patch</c>.</summary>
    public uint ModulePatch;
}
