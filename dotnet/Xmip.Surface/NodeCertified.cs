namespace Xmip.Surface;

/// <summary>The node <paramref name="Node"/> issued its certificate: the
/// chain <paramref name="Certificate"/>, its <paramref name="Key"/> and the
/// cluster's authority, <paramref name="Anchors"/>; or, where
/// <paramref name="Refused"/> says why, nothing issued
/// (<see cref="NodeCertificate.Issue"/>).</summary>
public sealed record NodeCertified(
    string Node, string Certificate, string Key, string Anchors, string Refused)
{
    /// <summary>Whether the node was issued its certificate.</summary>
    public bool Ok => Refused.Length == 0;
}
