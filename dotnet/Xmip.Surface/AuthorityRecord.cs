namespace Xmip.Surface;

/// <summary>One certificate the cluster's authority signed, as its register
/// keeps it (ADR-0034, amendment 2026-10-22): its <paramref name="Serial"/>,
/// hexadecimal; its <paramref name="Subject"/> and <paramref name="Names"/>;
/// what it serves, <paramref name="Purpose"/>; when it is valid, when it is
/// renewed, and its <paramref name="Standing"/> — current, renewal due,
/// expired or revoked; and, where it was revoked, when and for what
/// <paramref name="Reason"/>; or, where <paramref name="Refused"/> says why,
/// nothing done.</summary>
public sealed record AuthorityRecord(
    string Serial,
    string Subject,
    IReadOnlyList<string> Names,
    string Purpose,
    DateTimeOffset NotBefore,
    DateTimeOffset NotAfter,
    DateTimeOffset RenewAt,
    string Standing,
    DateTimeOffset? RevokedAt,
    string Reason,
    string Refused)
{
    /// <summary>Whether the act on it was done.</summary>
    public bool Ok => Refused.Length == 0;
}
