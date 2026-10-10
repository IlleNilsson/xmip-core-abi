namespace Xmip.Abi.Operate;

/// <summary>
/// One writer's audit chain as a verification walked it (section 9,
/// <c>xmip_audit_read_v1</c> asked <c>verify</c>; ADR-0070 clause 5): every
/// record carries the digest of the one before it in its writer's chain —
/// the node's, or the program's where no node writes it — and the walk says
/// the first place the chain breaks, or that it is whole.
/// </summary>
/// <param name="Writer">Whose chain: a node's location, or a program's
/// name.</param>
/// <param name="Records">How many of its records the walk read.</param>
/// <param name="Whole">Whether the chain is whole.</param>
/// <param name="Said">The verdict in one sentence, opening OK or FAILED,
/// naming where a record was deleted, changed or put out of order.</param>
public sealed record AuditChain(string Writer, long Records, bool Whole, string Said);
