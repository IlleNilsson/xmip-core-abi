namespace Xmip.Abi.Module;

/// <summary>
/// Every status the boundary can return. Section 3 of <c>xmip_module.h</c>,
/// shared with <c>xmip_operate.h</c> (ADR-0027 clause 1).
/// </summary>
/// <remarks>
/// The numbers are grouped by who is at fault, and the grouping is the useful
/// part: a caller error will fail again unchanged, a data error means the input
/// is wrong rather than the call, and an environment error may well succeed on
/// its own later.
/// </remarks>
public enum XmipStatus
{
    /// <summary>The call succeeded.</summary>
    Ok = 0,

    // Caller error. The call was wrong; repeating it unchanged will fail again.

    /// <summary>An argument was outside its contract.</summary>
    Invalid = -1,

    /// <summary>Well formed, and not implemented here.</summary>
    Unsupported = -2,

    /// <summary>The wrong lifecycle state for this call.</summary>
    State = -3,

    /// <summary>The thing asked for does not exist.</summary>
    NotFound = -4,

    // Data. The input is at fault, not the caller and not the environment.

    /// <summary>Not the standard it claims to be.</summary>
    Malformed = -10,

    /// <summary>Well formed, and violates the contract.</summary>
    Contract = -11,

    /// <summary>The stream ended mid-structure.</summary>
    Truncated = -12,

    // Environment.

    /// <summary>An input or output fault.</summary>
    Io = -20,

    /// <summary>The call did not answer in time.</summary>
    Timeout = -21,

    /// <summary>The peer refused, or is down.</summary>
    Unavailable = -22,

    /// <summary>Authentication or authorisation was refused.</summary>
    Auth = -23,

    /// <summary>A quota, limit or resource was exhausted.</summary>
    Capacity = -24,

    // Control.

    /// <summary>The host asked for cancellation.</summary>
    Cancelled = -30,

    /// <summary>Would block; not a failure.</summary>
    Again = -31,

    // Terminal. The module instance is unusable and must be destroyed.

    /// <summary>A defect in the module.</summary>
    Internal = -40,

    /// <summary>Unwinding was caught at the boundary.</summary>
    Panic = -41,
}
