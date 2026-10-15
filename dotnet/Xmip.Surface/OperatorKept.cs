namespace Xmip.Surface;

/// <summary>An operator's request for <paramref name="Person"/> over the key
/// <paramref name="Keeper"/> holds: <paramref name="Request"/>, PEM, to share
/// with whoever signs for the cluster's authority, the key made for it where
/// <paramref name="Made"/>, else the kept one asked again; or, where
/// <paramref name="Refused"/> says why, no request
/// (<see cref="OperatorCertificate.RequestKept"/>).</summary>
public sealed record OperatorKept(
    string Person, string Keeper, string Request, bool Made, string Refused)
{
    /// <summary>Whether the request was made.</summary>
    public bool Ok => Refused.Length == 0;
}
