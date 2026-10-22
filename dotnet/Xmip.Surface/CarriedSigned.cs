namespace Xmip.Surface;

/// <summary>A node's carried <paramref name="Request"/> signed by the
/// cluster's authority (ADR-0034, amendment 2026-10-22): the certificate's
/// <paramref name="Serial"/>, its <paramref name="Names"/>, until when it is
/// valid, and its chain, PEM, in <paramref name="Chain"/>, written to
/// <paramref name="Written"/> where a file was named, to carry back. Or,
/// where <paramref name="Refused"/> says why, nothing signed.</summary>
public sealed record CarriedSigned(
    string Request,
    string Serial,
    IReadOnlyList<string> Names,
    DateTimeOffset NotAfter,
    string? Written,
    string Chain,
    string Refused)
{
    /// <summary>Whether the request was signed.</summary>
    public bool Ok => Refused.Length == 0;
}
