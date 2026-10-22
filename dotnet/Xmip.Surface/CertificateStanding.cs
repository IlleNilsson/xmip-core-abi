namespace Xmip.Surface;

/// <summary>The leaf of the chain in the file
/// <paramref name="Certificate"/>: its <paramref name="Serial"/>,
/// <paramref name="Subject"/> and <paramref name="Names"/>, when it is
/// valid, when its renewal is due, and its <paramref name="Standing"/> —
/// current, renewal due or expired — what a node's operator reads to know
/// when to make a request (ADR-0034, amendment 2026-10-22). Or, where
/// <paramref name="Refused"/> says why, nothing read.</summary>
public sealed record CertificateStanding(
    string Certificate,
    string Serial,
    string Subject,
    IReadOnlyList<string> Names,
    DateTimeOffset NotBefore,
    DateTimeOffset NotAfter,
    DateTimeOffset RenewAt,
    string Standing,
    string Refused)
{
    /// <summary>Whether the certificate was read.</summary>
    public bool Ok => Refused.Length == 0;
}
