namespace Xmip.Abi.Operate;

/// <summary>
/// The Xmip operator boundary, as declared by <c>include/xmip_operate.h</c> in
/// xmip-core-abi: its version and the three symbols a runtime exports.
/// </summary>
/// <remarks>
/// ADR-0027: the boundary that drives Xmip from outside, versioned apart from
/// the module boundary a Module plugs into. ADR-0012 clause 1 applies here
/// too: the header is normative and this is not; where they differ, the
/// header is right and this is a defect.
/// </remarks>
public static class OperateAbi
{
    /// <summary>The operator boundary version this build speaks. Not the
    /// module boundary's version, on purpose.</summary>
    public const uint Version = 1u;

    /// <summary>The one symbol a runtime exports for the surfaces that watch.
    /// Section 1 of the header.</summary>
    public const string Entrypoint = "xmip_operate_v1";

    /// <summary>
    /// Wait until the runtime publishes a revision newer than the caller's.
    /// Separate from the table so version 1 remains binary-compatible.
    /// </summary>
    public const string ChangeEntrypoint = "xmip_wait_change_v1";

    /// <summary>Start a node from a saved configuration file. Section 6 of the
    /// header; a configurer's symbol, not part of the watcher's table.</summary>
    public const string StartEntrypoint = "xmip_start_v1";

    /// <summary>Validate configuration text without applying it. Section 6 of
    /// the header; ADR-0027 clause 9.</summary>
    public const string ValidateEntrypoint = "xmip_validate_v1";
}
