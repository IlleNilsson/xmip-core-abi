namespace Xmip.Abi.Operate;

/// <summary>
/// One audit record as the audit capability read it back (section 9,
/// <c>xmip_audit_read_v1</c>; ADR-0062, amendment 2026-09-29). Words stay
/// words — a phase, a severity — because a reader shows them as written.
/// </summary>
/// <param name="AuditId">The record's identifier.</param>
/// <param name="At">When, RFC 3339 to the nanosecond, as written.</param>
/// <param name="Program">The program that made it.</param>
/// <param name="Host">The machine it ran on.</param>
/// <param name="Process">Its process id, as written.</param>
/// <param name="Location">The scope its process declared, or null for a
/// program that serves none.</param>
/// <param name="Node">The node that scope is on, or null.</param>
/// <param name="Cluster">The cluster that scope is in, or null.</param>
/// <param name="Action">What it did.</param>
/// <param name="Phase">begin, execute, finished or failure.</param>
/// <param name="Severity">information, warning or error.</param>
/// <param name="Message">What it said, or null.</param>
/// <param name="Summary">What it says in one line: its message, else its
/// properties, cut short by the capability.</param>
/// <param name="Scope">The Message execution the act belongs to, key to text;
/// empty for a program's own act.</param>
/// <param name="Properties">Everything else it recorded, key to text.</param>
/// <param name="Hidden">Its process belongs to a run that declared itself
/// hidden (ADR-0028, amendment 2026-09-30); read only when the query included
/// what is hidden.</param>
public sealed record AuditEntry(
    string AuditId,
    string At,
    string Program,
    string Host,
    string Process,
    string? Location,
    string? Node,
    string? Cluster,
    string Action,
    string Phase,
    string Severity,
    string? Message,
    string Summary,
    IReadOnlyDictionary<string, string> Scope,
    IReadOnlyDictionary<string, string> Properties,
    bool Hidden);
