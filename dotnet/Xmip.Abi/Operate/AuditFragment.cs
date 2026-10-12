namespace Xmip.Abi.Operate;

/// <summary>
/// Bytes of the audit file a verification found no whole record in (section
/// 9, <c>xmip_audit_read_v1</c> asked <c>verify</c>; ADR-0070 clause 5): a
/// record whose write was interrupted. The next append keeps those bytes as
/// written and seals them with a comment line and a blank line, so they are in
/// no audit chain and the record after them is whole; the file's end, where a
/// write is still going on or none has sealed it yet, is not sealed.
/// </summary>
/// <param name="Offset">Where it starts, in bytes from the start of the
/// file.</param>
/// <param name="Length">How many bytes it is, its seal included.</param>
/// <param name="Sealed">Whether a later append sealed it.</param>
/// <param name="Said">The fragment in one sentence, opening FAILED for a
/// sealed one and UNFINISHED for the file's unfinished end.</param>
public sealed record AuditFragment(long Offset, long Length, bool Sealed, string Said);
