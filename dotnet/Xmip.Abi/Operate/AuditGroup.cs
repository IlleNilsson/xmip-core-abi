namespace Xmip.Abi.Operate;

/// <summary>
/// A group one step down the audit's drill from where a read stands: a
/// cluster or a host at the top, a cluster's nodes and its own programs, a
/// node's programs (section 9, <c>xmip_audit_read_v1</c>). Who a record is,
/// is the location its process declared — never its program's name.
/// </summary>
/// <param name="Kind">cluster, node, scope, program or host.</param>
/// <param name="Who">A scope, a program's name or a host's.</param>
/// <param name="Count">How many records it holds under the read's
/// filters.</param>
/// <param name="Warnings">How many of them are warnings.</param>
/// <param name="Errors">How many of them are errors.</param>
/// <param name="Latest">The newest one's time, as written.</param>
/// <param name="Hidden">Some record in it came from a run that declared itself
/// hidden: a reader that included what is hidden marks the group as
/// test.</param>
public sealed record AuditGroup(
    string Kind, string Who, int Count, int Warnings, int Errors, string Latest, bool Hidden);
