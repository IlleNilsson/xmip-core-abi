using Xmip.Abi.Operate;

namespace Xmip.Surface;

/// <summary>
/// A node's certificate for every .NET surface (ADR-0015, amendment
/// 2026-10-21): a fresh key and a certificate issued by the cluster's own
/// authority, for a node an environment template puts on a machine of its
/// own, for <c>xmip certificate --node</c> and
/// <c>New-XmipCertificate -Node</c>. The files, their names and protection,
/// the authority kept or made, and what the certificate is for are the
/// runtime's (<c>xmip_operate.h</c> section 22).
/// </summary>
public static class NodeCertificate
{
    /// <summary>The authority's name where a cluster's authority is made
    /// here: the cluster's.</summary>
    public static string AuthorityName(string cluster) => $"Xmip cluster {cluster} authority";

    /// <summary>
    /// The node <paramref name="node"/> of <paramref name="cluster"/> issued
    /// a fresh key and certificate by the authority kept in
    /// <paramref name="authority"/> — made there where none is — for every
    /// one of <paramref name="names"/> and its own, written into
    /// <paramref name="folder"/> as <c>node.pem</c>, <c>node.key</c> and
    /// <c>anchors.pem</c>.
    /// </summary>
    public static NodeCertified Issue(
        RuntimeCertificates certificates, string authority, string cluster, string node,
        IReadOnlyList<string> names, string folder)
    {
        ArgumentNullException.ThrowIfNull(certificates);
        ArgumentNullException.ThrowIfNull(cluster);

        return certificates.Issue(
                authority, AuthorityName(cluster), node, names, folder,
                out NodeIssued issued, out string no)
            ? new NodeCertified(node, issued.Certificate, issued.Key, issued.Anchors, string.Empty)
            : new NodeCertified(node, string.Empty, string.Empty, string.Empty, $"REFUSED: {no}");
    }
}
