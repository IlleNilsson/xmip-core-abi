namespace Xmip.Surface;

/// <summary>A key and a request made for <paramref name="Person"/>, at
/// <paramref name="Key"/> and <paramref name="Request"/>; or, where
/// <paramref name="Refused"/> says why, nothing written
/// (<see cref="OperatorCertificate.Request"/>).</summary>
public sealed record OperatorRequested(
    string Person, string Key, string Request, string Refused)
{
    /// <summary>Whether the files were written.</summary>
    public bool Ok => Refused.Length == 0;
}
