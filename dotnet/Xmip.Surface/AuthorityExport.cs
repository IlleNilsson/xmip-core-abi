namespace Xmip.Surface;

/// <summary>What was written out of the cluster's authority
/// (ADR-0034, amendment 2026-10-22): its certificate or its revocation list
/// in the <paramref name="Format"/> asked — pem, der or crl — at
/// <paramref name="Written"/>; or, for a backup, the whole authority into
/// the folder <paramref name="Written"/>, every file it
/// <paramref name="Copied"/>. Or, where <paramref name="Refused"/> says
/// why, nothing written.</summary>
public sealed record AuthorityExport(
    string Written, string Format, IReadOnlyList<string> Copied, string Refused)
{
    /// <summary>Whether it was written.</summary>
    public bool Ok => Refused.Length == 0;
}
