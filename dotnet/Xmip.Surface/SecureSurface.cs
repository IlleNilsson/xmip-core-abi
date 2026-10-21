using System.Net;
using Microsoft.Extensions.Configuration;

namespace Xmip.Surface;

/// <summary>
/// What a surface presents and what it trusts when it crosses a network
/// (ADR-0063 clause 1: internal traffic is mutual TLS, a loopback connection
/// inside one machine the one exception, said where it is made): the files a
/// host's document names, handed to the runtime, whose TLS is Xmip's own
/// (<c>xmip-core-library-tls</c>). A web host serves its pages over HTTPS in
/// it (<c>xmip_operate.h</c> section 21), presenting this certificate and
/// verifying the one a browser presents against these anchors; a
/// <see cref="RemoteOperator"/> hands the runtime the same files
/// (<see cref="Pem(out string, out string, out string)"/>), and the runtime
/// presents this certificate to a node's operate listener and checks the
/// node's against these anchors. The rule is here, once, for every .NET
/// surface; no .NET TLS stack reads these files.
/// </summary>
/// <remarks>
/// The certificate and its trust come the way <c>xmip-core-library-tls</c>
/// takes them (ADR-0033, ADR-0034): a PEM chain and its PEM private key, and
/// the anchors a peer's chain must reach, a PEM file of them — never the
/// operating system's trust store (ADR-0063). Where the files came from —
/// an internal CA, ACME, the Playground's roll authority — is provisioning's,
/// not this. In the host's <c>[Xmip]</c> table: <c>Certificate</c>,
/// <c>PrivateKey</c> and <c>TrustAnchor</c>, each else its environment
/// variable. Revocation is not checked here yet: the estate's revocation is
/// by held CRLs (ADR-0045), and none is configured for a surface.
/// </remarks>
public sealed partial class SecureSurface
{
    /// <summary>The key naming this end's PEM certificate chain, leaf first.</summary>
    public const string CertificateKey = "Xmip:Certificate";

    /// <summary>The key naming the PEM private key of that certificate.</summary>
    public const string PrivateKeyKey = "Xmip:PrivateKey";

    /// <summary>The key naming the PEM anchors a peer's chain must reach.</summary>
    public const string TrustAnchorKey = "Xmip:TrustAnchor";

    /// <summary>The environment variable when the document names no certificate.</summary>
    public const string CertificateVariable = "XMIP_CERTIFICATE";

    /// <summary>The environment variable when the document names no private key.</summary>
    public const string PrivateKeyVariable = "XMIP_PRIVATE_KEY";

    /// <summary>The environment variable when the document names no anchors.</summary>
    public const string TrustAnchorVariable = "XMIP_TRUST_ANCHOR";

    // What a remote surface naming no anchors is told, with a key in a file
    // or kept.
    private const string NoAnchors =
        $"REFUSED. A remote surface trusts a node by the anchors {TrustAnchorKey} "
        + $"or {TrustAnchorVariable} names, never the operating system's (ADR-0063)";

    private SecureSurface((string? Certificate, string? PrivateKey, string? TrustAnchor) paths)
    {
        Paths = paths;
    }

    /// <summary>Nothing presented and nothing trusted.</summary>
    public static SecureSurface None { get; } = new((null, null, null));

    /// <summary>The files this was read from, each null where none was
    /// named: a web host serves no HTTPS without the three, and a remote
    /// surface presents nothing without the first two.</summary>
    public (string? Certificate, string? PrivateKey, string? TrustAnchor) Paths { get; }

    /// <summary>Whether <paramref name="host"/> is this machine: <c>localhost</c>
    /// or a loopback address. <c>0.0.0.0</c>, <c>*</c> and <c>+</c> are every
    /// interface, and not loopback.</summary>
    public static bool IsLoopback(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return false;
        }

        string bare = host.Trim().TrimStart('[').TrimEnd(']');

        return string.Equals(bare, "localhost", StringComparison.OrdinalIgnoreCase)
            || (IPAddress.TryParse(bare, out IPAddress? address) && IPAddress.IsLoopback(address));
    }

    /// <summary>
    /// Whether a connection to <paramref name="address"/> may be made: HTTPS
    /// always, plain HTTP only on loopback (ADR-0063 clause 1). The reason
    /// says why not, in words.
    /// </summary>
    public static bool Permits(Uri address, out string reason)
    {
        ArgumentNullException.ThrowIfNull(address);

        return Permits(address.Scheme, address.Host, out reason);
    }

    /// <summary>
    /// The one rule for connecting and for binding alike: a scheme of
    /// <c>https</c> always, anything else only where <paramref name="host"/>
    /// is this machine (ADR-0063 clause 1).
    /// </summary>
    public static bool Permits(string scheme, string host, out string reason)
    {
        if (string.Equals(scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || IsLoopback(host))
        {
            reason = string.Empty;
            return true;
        }

        reason = $"REFUSED. {host} is plain {scheme} beyond this machine; Xmip's own traffic "
            + "is TLS, loopback the one exception (ADR-0063 clause 1)";
        return false;
    }

    /// <summary>
    /// The certificate, key and anchors the configuration names, each key
    /// else its environment variable; paths resolve against
    /// <paramref name="basePath"/>. None of them named is <see cref="None"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">A certificate without its
    /// key or the reverse, or a file that is not there; the message names
    /// it.</exception>
    public static SecureSurface From(IConfiguration configuration, string basePath)
    {
        return From(configuration, basePath, null);
    }

    /// <summary>
    /// As <see cref="From(IConfiguration, string)"/>, where this end's key
    /// may be kept by its platform's <paramref name="keeper"/> instead of a
    /// file: a configuration that names a certificate and no private key
    /// presents that chain with the key the keeper holds
    /// (<see cref="Kept"/>). A configuration naming a private key reads it,
    /// keeper or not.
    /// </summary>
    /// <exception cref="InvalidOperationException">As
    /// <see cref="From(IConfiguration, string)"/>.</exception>
    public static SecureSurface From(
        IConfiguration configuration, string basePath, IOperatorKeeper? keeper)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        string? certificate = Named(configuration, CertificateKey, CertificateVariable, basePath);
        string? privateKey = Named(configuration, PrivateKeyKey, PrivateKeyVariable, basePath);
        string? trustAnchor = Named(configuration, TrustAnchorKey, TrustAnchorVariable, basePath);

        return keeper is not null && certificate is not null && privateKey is null
            ? Kept(certificate, trustAnchor, keeper)
            : Load(certificate, privateKey, trustAnchor);
    }

    /// <summary>
    /// The certificate chain at <paramref name="certificatePath"/> with the key
    /// at <paramref name="privateKeyPath"/>, and the anchors at
    /// <paramref name="trustAnchorPath"/>; all PEM, any of them null. What
    /// they hold the runtime reads, and refuses in words, as it is handed
    /// them.
    /// </summary>
    /// <exception cref="InvalidOperationException">As
    /// <see cref="From(IConfiguration, string)"/>.</exception>
    public static SecureSurface Load(
        string? certificatePath, string? privateKeyPath, string? trustAnchorPath)
    {
        if ((certificatePath is null) != (privateKeyPath is null))
        {
            throw new InvalidOperationException(
                $"{CertificateKey} and {PrivateKeyKey} are named together or not at all");
        }

        foreach (string? named in new[] { certificatePath, privateKeyPath, trustAnchorPath })
        {
            if (named is not null && !File.Exists(named))
            {
                throw new InvalidOperationException($"{named} is not there");
            }
        }

        return new SecureSurface((certificatePath, privateKeyPath, trustAnchorPath));
    }

    /// <summary>
    /// The certificate chain, its key and the anchors as the PEM text
    /// <c>xmip-core-library-tls</c> takes them, for a connection the runtime
    /// makes: a remote surface's to a node's operate listener, which is
    /// mutual TLS always (ADR-0063). Null with all three read; else what is
    /// missing, in words.
    /// </summary>
    public string? Pem(out string certificate, out string privateKey, out string trustAnchor)
    {
        certificate = privateKey = trustAnchor = string.Empty;

        if (Paths.Certificate is null || Paths.PrivateKey is null)
        {
            return $"REFUSED. A remote surface presents a certificate: name {CertificateKey} "
                + $"and {PrivateKeyKey}, or {CertificateVariable} and {PrivateKeyVariable} "
                + "(ADR-0063 clause 1)";
        }

        if (Paths.TrustAnchor is null)
        {
            return NoAnchors;
        }

        certificate = File.ReadAllText(Paths.Certificate);
        privateKey = File.ReadAllText(Paths.PrivateKey);
        trustAnchor = File.ReadAllText(Paths.TrustAnchor);
        return null;
    }

    private static string? Named(
        IConfiguration configuration, string key, string variable, string basePath)
    {
        string? named = configuration[key];

        if (string.IsNullOrWhiteSpace(named))
        {
            named = Environment.GetEnvironmentVariable(variable);
        }

        return string.IsNullOrWhiteSpace(named) ? null : TomlDocument.Resolve(named, basePath);
    }
}
