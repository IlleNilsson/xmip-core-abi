namespace Xmip.Abi.Operate;

/// <summary>
/// What became of one audit record, and in words where it went when that was
/// not the audit sink — the operating system's log and why, or why nothing
/// kept it.
/// </summary>
/// <param name="Kept">Where the record is; null when nothing kept it.</param>
/// <param name="Said">Where and why, when it is not in the sink; empty
/// otherwise.</param>
public sealed record AuditOutcome(AuditKept? Kept, string Said);
