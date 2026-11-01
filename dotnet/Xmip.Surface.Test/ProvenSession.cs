using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using Xmip.Abi.Operate;

namespace Xmip.Surface.Test;

/// <summary>
/// A browser's session as a web host takes it, over the runtime's own TLS
/// (section 21): a listener under a test authority, a client presenting a
/// certificate or none, and the session the runtime accepted, whose caller
/// its gate proved and holds (ADR-0009, amendment 2026-11-01) — the one way
/// a test comes by a caller the runtime holds for a certificate, as a host
/// does. Disposing ends the session, the client and the listener.
/// </summary>
internal sealed class ProvenSession : IDisposable
{
    private readonly SecureSurfaceListener listener;

    private readonly TcpClient tcp;

    private readonly SslStream client;

    public ProvenSession(TestAuthority authority, X509Certificate2? certificate)
    {
        (string hostCertificate, string hostKey) = authority.Server();
        listener = RuntimeLibrary.Rules.Hosting.Open(
            Protection.Tls,
            "127.0.0.1:0",
            File.ReadAllText(hostCertificate),
            File.ReadAllText(hostKey),
            File.ReadAllText(authority.Anchor),
            out string reason) ?? throw new InvalidOperationException(reason);

        string[] address = listener.Address.Split(':');
        tcp = new TcpClient(address[0], int.Parse(address[1], null));
        byte[] trusted = X509CertificateLoader.LoadCertificateFromFile(authority.Anchor).RawData;
        client = new SslStream(tcp.GetStream(), false, (_, offered, _, _) =>
        {
            // The host is checked against the test authority alone.
            using X509Chain chain = new();
            using X509Certificate2 root = X509CertificateLoader.LoadCertificate(trusted);
            using X509Certificate2? host = offered is null ? null : new X509Certificate2(offered);
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            chain.ChainPolicy.CustomTrustStore.Add(root);
            return host is not null && chain.Build(host);
        });
        Task handshake = client.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
        {
            TargetHost = "127.0.0.1",
            ClientCertificates = certificate is null ? null : [certificate],
            ApplicationProtocols = [SslApplicationProtocol.Http11],
        });

        SecureSurfaceAccepted accepted = listener.Accept();
        handshake.GetAwaiter().GetResult();
        Session = accepted.Session ?? throw new InvalidOperationException(accepted.Said);
    }

    /// <summary>The session the runtime accepted.</summary>
    public SecureSurfaceSession Session { get; }

    /// <summary>Stop the listener: every caller its sessions proved is
    /// revoked.</summary>
    public void Stop()
    {
        listener.Stop();
    }

    public void Dispose()
    {
        Session.Dispose();
        client.Dispose();
        tcp.Dispose();
        listener.Dispose();
    }

    /// <summary>A certificate and its key as a client presents them: Windows's
    /// TLS stack presents no key held only in memory, which a PEM key
    /// becomes, so the pair goes through PKCS#12 first.</summary>
    public static X509Certificate2 Presentable((string Certificate, string Key) pair)
    {
        using X509Certificate2 paired =
            X509Certificate2.CreateFromPemFile(pair.Certificate, pair.Key);

        return X509CertificateLoader.LoadPkcs12(paired.Export(X509ContentType.Pkcs12), null);
    }
}
