using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Xmip.Surface.Test;

/// <summary>
/// A certificate authority for one test, written as the PEM files a host's
/// document names (ADR-0034 clause 4: the tests exercise usage with a
/// stand-in authority and are honest that it issues nothing real). Its
/// anchor, a server certificate for this machine and a client certificate
/// land in a directory of their own, deleted with the authority.
/// </summary>
internal sealed class TestAuthority : IDisposable
{
    private readonly ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

    private readonly X509Certificate2 anchor;

    public TestAuthority(string name)
    {
        Directory = Path.Combine(Path.GetTempPath(), $"xmip-tls-{name}-{Guid.NewGuid():n}");
        System.IO.Directory.CreateDirectory(Directory);

        CertificateRequest request = new($"CN=Xmip test anchor {name}", key, HashAlgorithmName.SHA256);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
        request.CertificateExtensions.Add(
            new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        anchor = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow.AddDays(1));

        Anchor = Path.Combine(Directory, "anchor.pem");
        File.WriteAllText(Anchor, anchor.ExportCertificatePem());
    }

    /// <summary>Where this authority's files are.</summary>
    public string Directory { get; }

    /// <summary>The anchor's PEM, what a peer trusts.</summary>
    public string Anchor { get; }

    /// <summary>A server certificate for 127.0.0.1 and localhost, as a
    /// certificate and a key file.</summary>
    public (string Certificate, string PrivateKey) Server()
    {
        return Issue("server", SurfaceTls.ServerAuthentication);
    }

    /// <summary>A client certificate for a remote surface.</summary>
    public (string Certificate, string PrivateKey) Client()
    {
        return Issue("client", SurfaceTls.ClientAuthentication);
    }

    public void Dispose()
    {
        anchor.Dispose();
        key.Dispose();

        try
        {
            System.IO.Directory.Delete(Directory, recursive: true);
        }
        catch (IOException)
        {
            // A file the platform still holds; the temp directory keeps it.
        }
    }

    private (string Certificate, string PrivateKey) Issue(string name, string purpose)
    {
        using ECDsa leafKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        CertificateRequest request = new($"CN=xmip-{name}", leafKey, HashAlgorithmName.SHA256);
        SubjectAlternativeNameBuilder names = new();
        names.AddIpAddress(IPAddress.Loopback);
        names.AddDnsName("localhost");
        request.CertificateExtensions.Add(names.Build());
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension([new Oid(purpose)], false));
        request.CertificateExtensions.Add(
            X509AuthorityKeyIdentifierExtension.CreateFromCertificate(anchor, true, false));

        using X509Certificate2 issued = request.Create(
            anchor,
            DateTimeOffset.UtcNow.AddMinutes(-30),
            DateTimeOffset.UtcNow.AddHours(12),
            RandomNumberGenerator.GetBytes(16));

        string certificate = Path.Combine(Directory, $"{name}.pem");
        string privateKey = Path.Combine(Directory, $"{name}.key");
        File.WriteAllText(certificate, issued.ExportCertificatePem());
        File.WriteAllText(privateKey, leafKey.ExportPkcs8PrivateKeyPem());

        return (certificate, privateKey);
    }
}
