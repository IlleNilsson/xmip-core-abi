namespace Xmip.Abi.Operate;

/// <summary>
/// The lifecycle phase of an audited act — <c>doc/audit-record.md</c> in
/// xmip-core-audit: <c>Begin -&gt; Execute -&gt; Finished</c> or
/// <c>Begin -&gt; Execute -&gt; Failure</c>. The values are the header's
/// <c>XMIP_PHASE_*</c> (section 9).
/// </summary>
public enum AuditPhase
{
    /// <summary>The act began.</summary>
    Begin = 0,

    /// <summary>The act is under way.</summary>
    Execute = 1,

    /// <summary>The act finished.</summary>
    Finished = 2,

    /// <summary>The act failed. Always recorded; that is not policy.</summary>
    Failure = 3,
}
