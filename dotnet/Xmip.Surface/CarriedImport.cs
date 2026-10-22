namespace Xmip.Surface;

/// <summary>A carried certificate imported on its node (ADR-0034, amendment
/// 2026-10-22): <paramref name="Certificate"/>, <paramref name="Key"/> and
/// <paramref name="Anchors"/> written, its <paramref name="Serial"/>, until
/// when it is valid and when its renewal is due; the SHA-256
/// <paramref name="AnchorFingerprint"/> of the authority, and whether that
/// anchor was taken from the chain, <paramref name="AnchorTaken"/>, the
/// node holding none — then compare the fingerprint by another way. Or,
/// where <paramref name="Refused"/> says why, nothing changed.</summary>
public sealed record CarriedImport(
    string Certificate,
    string Key,
    string Anchors,
    string Serial,
    DateTimeOffset NotAfter,
    DateTimeOffset RenewAt,
    string AnchorFingerprint,
    bool AnchorTaken,
    string Refused)
{
    /// <summary>Whether the certificate was imported.</summary>
    public bool Ok => Refused.Length == 0;
}
