namespace Xmip.Abi.Operate;

/// <summary>
/// What an audit read found (section 9, <c>xmip_audit_read_v1</c>; ADR-0062,
/// amendment 2026-09-29): which file it read, how many records that holds and
/// how many matched, the page asked for, the groups one step down, the
/// actions there are to choose from, and each writer's audit chain where the
/// read asked to verify.
/// </summary>
/// <param name="File">The file read; empty where no audit directory is
/// stated and records go to the operating system's log.</param>
/// <param name="Read">How many records the file holds.</param>
/// <param name="Matched">How many matched the read.</param>
/// <param name="Offset">Where the page starts among them.</param>
/// <param name="Limit">How long a page the read asked for.</param>
/// <param name="Records">The page.</param>
/// <param name="Groups">One step down the drill.</param>
/// <param name="Actions">Every action where the read stands.</param>
/// <param name="Chains">Where the read asked <c>verify</c>, the audit chain of
/// each writer of the records it matched, walked whole (ADR-0070 clause 5);
/// empty otherwise.</param>
/// <param name="Columns">The columns a record sorts by, as the capability
/// names them, in the order a reader shows them.</param>
/// <param name="Severities">The severities, least first, as the capability
/// names them.</param>
public sealed record AuditRead(
    string File,
    int Read,
    int Matched,
    int Offset,
    int Limit,
    IReadOnlyList<AuditEntry> Records,
    IReadOnlyList<AuditGroup> Groups,
    IReadOnlyList<string> Actions,
    IReadOnlyList<AuditChain> Chains,
    IReadOnlyList<string> Columns,
    IReadOnlyList<string> Severities);
