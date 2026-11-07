namespace Xmip.Abi.Operate;

/// <summary>
/// What became of one audit record, one of two: something kept it — the
/// sink, the operating system's log, or policy that suppressed it — or
/// nothing did. Xmip's own fixed answer, so a union: a switch over it names
/// both.
/// </summary>
public readonly union AuditOutcome(AuditOutcome.Kept, AuditOutcome.Refused)
{
    /// <summary>The record is kept, at <paramref name="Where"/>.</summary>
    /// <param name="Where">Where the record is.</param>
    /// <param name="Said">Where and why, when it is not in the sink — the
    /// operating system's log and why; empty otherwise.</param>
    public sealed record Kept(AuditKept Where, string Said);

    /// <summary>Nothing kept the record.</summary>
    /// <param name="Said">Why, in the capability's words.</param>
    public sealed record Refused(string Said);
}
