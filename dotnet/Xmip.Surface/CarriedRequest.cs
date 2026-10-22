namespace Xmip.Surface;

/// <summary>A node's request for its certificate, made where no network
/// reaches the cluster's authority (ADR-0034, amendment 2026-10-22): the
/// file <paramref name="Request"/>, <c>node.csr</c>, to carry to the
/// authority; the key <paramref name="Key"/>, <c>node.next.key</c>, which
/// stays; and the <paramref name="Names"/> asked for. Or, where
/// <paramref name="Refused"/> says why, nothing written.</summary>
public sealed record CarriedRequest(
    string Node, string Request, string Key, IReadOnlyList<string> Names, string Refused)
{
    /// <summary>Whether the request was made.</summary>
    public bool Ok => Refused.Length == 0;
}
