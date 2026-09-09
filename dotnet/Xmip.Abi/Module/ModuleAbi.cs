namespace Xmip.Abi.Module;

/// <summary>
/// The Xmip module boundary, as declared by <c>include/xmip_module.h</c> in
/// xmip-core-abi: the version, the entrypoint, and the file the host looks for.
/// </summary>
/// <remarks>
/// ADR-0012 clause 1: the header is normative and this is not. Nothing here
/// may be relied on where the header disagrees — if the two ever differ, the
/// header is right and this is a defect.
/// </remarks>
public static class ModuleAbi
{
    /// <summary>The handshake version this build speaks.</summary>
    public const uint AbiVersion = 1u;

    /// <summary>The one symbol every module exports.</summary>
    public const string Entrypoint = "xmip_create_module_v1";

    /// <summary>
    /// Platform naming for a loadable module, from section 1 of the header:
    /// the platform prefix where the platform has one, the platform suffix
    /// always.
    /// </summary>
    public static string LibraryFileName(string moduleName)
    {
        return OperatingSystem.IsWindows() ? $"{moduleName}.dll"
            : OperatingSystem.IsMacOS() ? $"lib{moduleName}.dylib"
            : $"lib{moduleName}.so";
    }
}
