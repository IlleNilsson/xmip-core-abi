using System.Runtime.InteropServices;

namespace Xmip.Abi.Module;

/// <summary>
/// What the module says it is. Section 4 of the header: the three name parts
/// are the same three parts as the repository name under ADR-0011, and the
/// host rejects a module whose descriptor disagrees with the artifact that
/// asked for it.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct XmipModuleDescriptor
{
    /// <summary>The boundary version the module was built against.</summary>
    public uint AbiVersion;

    /// <summary>The provider part of the name; <c>core</c> for Xmip's own.</summary>
    public XmipStr Provider;

    /// <summary>The module part of the name.</summary>
    public XmipStr Module;

    /// <summary>The standard part of the name. Empty only when the provider
    /// is <c>core</c>.</summary>
    public XmipStr Standard;

    /// <summary>The trait version the module was built against.</summary>
    public uint TraitMajor;

    /// <summary>The trait version the module was built against.</summary>
    public uint TraitMinor;

    /// <summary>The module's own version; no compatibility meaning.</summary>
    public uint ModuleMajor;

    /// <summary>The module's own version; no compatibility meaning.</summary>
    public uint ModuleMinor;

    /// <summary>The module's own version; no compatibility meaning.</summary>
    public uint ModulePatch;
}
