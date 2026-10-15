namespace Xmip.Surface;

/// <summary>The request at <paramref name="Request"/> signed: its chain,
/// PEM, leaf first, in <paramref name="Chain"/>, and written to
/// <paramref name="Written"/> where a file was named; or, where
/// <paramref name="Refused"/> says why, nothing signed
/// (<see cref="OperatorCertificate.Sign"/>).</summary>
public sealed record OperatorSigned(
    string Request, string? Written, string Chain, string Refused)
{
    /// <summary>Whether the request was signed.</summary>
    public bool Ok => Refused.Length == 0;
}
