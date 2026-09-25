namespace Xmip.Abi.Operate;

/// <summary>
/// How serious an audited act is, independent of its phase —
/// <c>doc/audit-record.md</c> in xmip-core-audit. The values are the header's
/// <c>XMIP_SEVERITY_*</c> (section 9).
/// </summary>
public enum AuditSeverity
{
    /// <summary>Normal execution and successful completion.</summary>
    Information = 0,

    /// <summary>Recoverable, degraded or exceptional; execution may continue.</summary>
    Warning = 1,

    /// <summary>A failure, or a condition preventing continuation. Always
    /// recorded; that is not policy.</summary>
    Error = 2,
}
