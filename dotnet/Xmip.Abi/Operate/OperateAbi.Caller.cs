namespace Xmip.Abi.Operate;

// Section 18's callers: a caller the runtime proved, held behind an opaque
// handle a host carries to the gate in place of a name (ADR-0009, amendment
// 2026-11-01).
public static partial class OperateAbi
{
    /// <summary>Another holder of a held caller, a handle of its own:
    /// section 18.</summary>
    public const string CallerHoldEntrypoint = "xmip_caller_hold_v1";

    /// <summary>A held caller released, no one after: section 18.</summary>
    public const string CallerReleaseEntrypoint = "xmip_caller_release_v1";

    /// <summary>Who a held caller is, for display: section 18.</summary>
    public const string CallerNamedEntrypoint = "xmip_caller_named_v1";

    /// <summary>The caller this process is, the account the operating
    /// system says it runs as, held: section 18, a node's build.</summary>
    public const string CallerProcessEntrypoint = "xmip_caller_process_v1";
}
