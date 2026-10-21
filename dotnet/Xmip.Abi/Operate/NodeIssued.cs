namespace Xmip.Abi.Operate;

/// <summary>Where <see cref="RuntimeCertificates.Issue"/> wrote a node's
/// certificate: the chain <paramref name="Certificate"/>, leaf first, its key
/// <paramref name="Key"/>, and <paramref name="Anchors"/>, the cluster's
/// authority, each a private file (<c>xmip_operate.h</c> section 22).</summary>
public sealed record NodeIssued(string Certificate, string Key, string Anchors);
