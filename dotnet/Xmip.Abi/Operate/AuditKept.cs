namespace Xmip.Abi.Operate;

/// <summary>
/// What became of one audit record (ADR-0062). The values are the header's
/// <c>XMIP_KEPT_*</c> (section 9).
/// </summary>
public enum AuditKept
{
    /// <summary>Policy suppressed it. Never a failure.</summary>
    Suppressed = 0,

    /// <summary>The audit sink persisted it.</summary>
    Persisted = 1,

    /// <summary>The sink could not, and the operating system's log holds
    /// it.</summary>
    OperatingSystem = 2,
}
