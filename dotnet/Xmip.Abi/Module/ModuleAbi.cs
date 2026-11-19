namespace Xmip.Abi.Module;

/// <summary>
/// The Xmip module boundary, as declared by <c>include/xmip_module.h</c> in
/// xmip-core-abi: the version and the entrypoint. How a platform names a
/// module's library is abi's Rust (<c>abi::boundaries</c>), asked through the
/// operating library's <c>xmip_boundaries_v1</c>.
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
}
