using System.Net;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Configuration;

namespace Xmip.Surface;

/// <summary>
/// What a surface presents and what it trusts when it crosses a network
/// (ADR-0063 clause 1: internal traffic is mutual TLS, a loopback connection
/// inside one machine the one exception, said where it is made). A web host
/// serves its pages and its surface hub over HTTPS with this certificate and
/// checks the one a <see cref="RemoteOperator"/> presents; a remote surface
/// presents this certificate and checks the host's. The rule is here, once,
/// for both ends and every .NET surface.
/// </summary>
/// <remarks>
/// The certificate and its trust come the way <c>xmip-core-library-tls</c>
/// takes them (ADR-0033, ADR-0034): a PEM chain and its PEM private key, and
/// the anchors a peer's chain must reach — a PEM file of them, else the
/// operating system's trust store, because the organizations Xmip is aimed
/// at run their own certificate authorities. Where the files came from —
/// an internal CA, ACME, the Playground's stand-in — is provisioning's, not
/// this. In the host's <c>[Xmip]</c> table: <c>Certificate</c>,
/// <c>PrivateKey</c> and <c>TrustAnchor</c>, each else its environment
/// variable. Revocation is not checked here yet: the estate's revocation is
/// by held CRLs (ADR-0045), and none is configured for a surface.
/// </remarks>
public sealed class SurfaceTls
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

    /// <summary>The purpose a server's certificate must be for.</summary>
    public const string ServerAuthentication = "1.3.6.1.5.5.7.3.1";

    /// <summary>The purpose a client's certificate must be for.</summary>
    public const string ClientAuthentication = "1.3.6.1.5.5.7.3.2";

    private SurfaceTls(
        X509Certificate2? certificate,
        X509Certificate2Collection chain,
        X509Certificate2Collection anchors)
    {
        Certificate = certificate;
        Chain = chain;
        Anchors = anchors;
    }

    /// <summary>Nothing presented, and the operating system's anchors trusted.</summary>
    public static SurfaceTls None { get; } = new(null, [], []);

    /// <summary>What this end presents, with its private key; null where
    /// nothing is configured, and then a web host serves no HTTPS and a
    /// remote surface presents nothing.</summary>
    public X509Certificate2? Certificate { get; }

    /// <summary>The certificates between <see cref="Certificate"/> and its
    /// anchor, as the chain file lists them after the leaf.</summary>
    public X509Certificate2Collection Chain { get; }

    /// <summary>The anchors a peer's chain must reach; empty is the
    /// operating system's trust store.</summary>
    public X509Certificate2Collection Anchors { get; }

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
    /// key or the reverse, or a file that cannot be read as PEM; the message
    /// names the file.</exception>
    public static SurfaceTls From(IConfiguration configuration, string basePath)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return Load(
            Named(configuration, CertificateKey, CertificateVariable, basePath),
            Named(configuration, PrivateKeyKey, PrivateKeyVariable, basePath),
            Named(configuration, TrustAnchorKey, TrustAnchorVariable, basePath));
    }

    /// <summary>
    /// The certificate chain at <paramref name="certificatePath"/> with the key
    /// at <paramref name="privateKeyPath"/>, and the anchors at
    /// <paramref name="trustAnchorPath"/>; all PEM, any of them null.
    /// </summary>
    /// <exception cref="InvalidOperationException">As <see cref="From"/>.</exception>
    public static SurfaceTls Load(
        string? certificatePath, string? privateKeyPath, string? trustAnchorPath)
    {
        if ((certificatePath is null) != (privateKeyPath is null))
        {
            throw new InvalidOperationException(
                $"{CertificateKey} and {PrivateKeyKey} are named together or not at all");
        }

        X509Certificate2Collection anchors = trustAnchorPath is null ? [] : Pem(trustAnchorPath);

        if (trustAnchorPath is not null && anchors.Count == 0)
        {
            throw new InvalidOperationException($"{trustAnchorPath} holds no certificate");
        }

        if (certificatePath is null || privateKeyPath is null)
        {
            return new SurfaceTls(null, [], anchors);
        }

        X509Certificate2Collection chain = Pem(certificatePath);

        if (chain.Count == 0)
        {
            throw new InvalidOperationException($"{certificatePath} holds no certificate");
        }

        X509Certificate2 leaf;

        try
        {
            using X509Certificate2 paired =
                X509Certificate2.CreateFromPemFile(certificatePath, privateKeyPath);

            // Windows's TLS stack will not present a key held only in memory,
            // which is what a PEM key becomes; a PKCS#12 round trip gives it one
            // the stack can use, as Kestrel's own PEM loader does.
            leaf = X509CertificateLoader.LoadPkcs12(paired.Export(X509ContentType.Pkcs12), null);
        }
        catch (CryptographicException unreadable)
        {
            throw new InvalidOperationException(
                $"{certificatePath} with {privateKeyPath}: {unreadable.Message}", unreadable);
        }

        chain.RemoveAt(0);

        return new SurfaceTls(leaf, chain, anchors);
    }

    /// <summary>
    /// Whether the certificate a peer presented reaches one of
    /// <see cref="Anchors"/> and is for <paramref name="purpose"/> —
    /// <see cref="ServerAuthentication"/> or <see cref="ClientAuthentication"/>.
    /// Where the peer is a server, the platform's own check that it is the
    /// host asked for must have passed too: <paramref name="errors"/> is what
    /// the platform found, and only its chain verdict is replaced by this one.
    /// </summary>
    public bool Accepts(
        X509Certificate? presented,
        X509Chain? sent,
        SslPolicyErrors errors,
        string purpose,
        out string reason)
    {
        if (presented is null || errors.HasFlag(SslPolicyErrors.RemoteCertificateNotAvailable))
        {
            reason = "the peer presented no certificate";
            return false;
        }

        if (errors.HasFlag(SslPolicyErrors.RemoteCertificateNameMismatch))
        {
            reason = $"{presented.Subject} does not name the host asked for";
            return false;
        }

        using X509Certificate2 peer = X509CertificateLoader.LoadCertificate(presented.GetRawCertData());
        using X509Chain chain = new();
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.ApplicationPolicy.Add(new Oid(purpose));

        if (Anchors.Count > 0)
        {
            chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            chain.ChainPolicy.CustomTrustStore.AddRange(Anchors);
        }

        if (sent is not null)
        {
            foreach (X509ChainElement element in sent.ChainElements)
            {
                chain.ChainPolicy.ExtraStore.Add(element.Certificate);
            }
        }

        if (chain.Build(peer))
        {
            reason = string.Empty;
            return true;
        }

        string said = string.Join(
            "; ", chain.ChainStatus.Select(status => status.StatusInformation.Trim()));
        reason = $"{peer.Subject} is not trusted: {said}";
        return false;
    }

    private static X509Certificate2Collection Pem(string path)
    {
        X509Certificate2Collection read = [];

        try
        {
            read.ImportFromPemFile(path);
        }
        catch (Exception unreadable) when (unreadable is CryptographicException or IOException)
        {
            throw new InvalidOperationException($"{path}: {unreadable.Message}", unreadable);
        }

        return read;
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
