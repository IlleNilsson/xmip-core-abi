namespace Xmip.Abi.Operate;

/// <summary>
/// Where the runtime wrote a web host's certificate under an authority kept
/// in a folder (section 21, <see cref="RuntimeSecureSurface.Issue"/>).
/// </summary>
/// <param name="Certificate">The chain, <c>host.pem</c>, leaf first.</param>
/// <param name="Key">Its private key, <c>host.key</c>, a private file.</param>
/// <param name="Anchor">The authority's certificate, <c>authority.pem</c>:
/// what a browser trusts the host by, and what the host verifies a
/// browser's certificate against.</param>
public sealed record SecureSurfaceIssued(string Certificate, string Key, string Anchor);
