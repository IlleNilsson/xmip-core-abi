namespace Xmip.Surface;

/// <summary>The cluster's authority kept in the folder
/// <paramref name="Authority"/> (ADR-0034, amendment 2026-10-22): its
/// <paramref name="Subject"/>, when it is valid, the SHA-256
/// <paramref name="Fingerprint"/> of its certificate — what is compared by
/// another way before it is trusted — the plain-HTTP address it publishes
/// at, <paramref name="Published"/>, empty where it publishes nowhere; its
/// revocation list's number, next update and how many it names; and every
/// certificate it signed, newest first, <paramref name="Issued"/>. Or,
/// where <paramref name="Refused"/> says why, nothing read.</summary>
public sealed record AuthorityState(
    string Authority,
    string Subject,
    DateTimeOffset NotBefore,
    DateTimeOffset NotAfter,
    string Fingerprint,
    string Published,
    long ListNumber,
    DateTimeOffset ListNextUpdate,
    int Revoked,
    IReadOnlyList<AuthorityRecord> Issued,
    string Refused)
{
    /// <summary>Whether the authority was read.</summary>
    public bool Ok => Refused.Length == 0;
}
