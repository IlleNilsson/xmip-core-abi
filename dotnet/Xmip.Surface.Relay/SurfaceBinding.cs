using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.Configuration;

namespace Xmip.Surface.Relay;

/// <summary>
/// Where a web host listens, and how (ADR-0063 clause 1): every address that
/// is not this machine is HTTPS with the host's certificate, a client
/// certificate is asked for and checked against the host's anchors, and plain
/// HTTP is bound on loopback only — the one exception, which the host says
/// where it binds it. The rule itself is <see cref="SurfaceTls.Permits(string, string, out string)"/>.
/// </summary>
public static class SurfaceBinding
{
    /// <summary>Where Kestrel binds by default when nothing says otherwise.</summary>
    private const string KestrelDefault = "http://localhost:5000";

    /// <summary>
    /// Every address the configuration binds: each <c>Kestrel:Endpoints:*:Url</c>,
    /// else the <c>urls</c> key, else Kestrel's own default — the order Kestrel
    /// itself takes them in.
    /// </summary>
    public static IReadOnlyList<string> Addresses(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        string[] endpoints =
        [
            .. configuration.GetSection("Kestrel:Endpoints").GetChildren()
                .Select(endpoint => endpoint["Url"])
                .Where(url => !string.IsNullOrWhiteSpace(url))
                .Select(url => url!),
        ];

        if (endpoints.Length > 0)
        {
            return endpoints;
        }

        string? urls = configuration["urls"];

        return string.IsNullOrWhiteSpace(urls)
            ? [KestrelDefault]
            : urls.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    /// <summary>
    /// Refuse what may not be bound — plain HTTP beyond this machine, HTTPS
    /// with no certificate to present — and answer the plain loopback
    /// addresses, for the host to say it binds them.
    /// </summary>
    /// <exception cref="InvalidOperationException">An address that may not be
    /// bound, with the reason.</exception>
    public static IReadOnlyList<string> Check(IEnumerable<string> addresses, SurfaceTls tls)
    {
        ArgumentNullException.ThrowIfNull(addresses);
        ArgumentNullException.ThrowIfNull(tls);

        List<string> plain = [];

        foreach (string address in addresses)
        {
            (string scheme, string host) = Parts(address);
            bool secure = string.Equals(scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);

            if (secure && tls.Certificate is null)
            {
                throw new InvalidOperationException(
                    $"REFUSED. {address} is HTTPS and {SurfaceTls.CertificateKey} names no "
                    + "certificate to present (ADR-0063 clause 1)");
            }

            if (!SurfaceTls.Permits(scheme, host, out string refused))
            {
                throw new InvalidOperationException(
                    $"{refused}. Bind https:// with {SurfaceTls.CertificateKey} and "
                    + SurfaceTls.PrivateKeyKey);
            }

            if (!secure)
            {
                plain.Add(address);
            }
        }

        return plain;
    }

    /// <summary>
    /// Every HTTPS address this host binds presents <paramref name="tls"/>'s
    /// certificate and asks the caller for one: a browser may decline, and
    /// then reads the pages; a certificate presented is checked against the
    /// anchors, and one they do not reach ends the handshake and is told to
    /// <paramref name="refused"/>. The hub itself takes no caller without one
    /// (<see cref="SurfaceRelayExtensions.MapXmipSurfaceHub"/>).
    /// </summary>
    public static IWebHostBuilder UseXmipTls(
        this IWebHostBuilder host, SurfaceTls tls, Action<string>? refused = null)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(tls);

        return tls.Certificate is not { } presented ? host : host.ConfigureKestrel(kestrel => kestrel.ConfigureHttpsDefaults(https =>
        {
            https.ServerCertificate = presented;
            https.ServerCertificateChain = tls.Chain;
            https.ClientCertificateMode = ClientCertificateMode.AllowCertificate;
            https.ClientCertificateValidation = (certificate, chain, errors) =>
            {
                if (tls.Accepts(
                    certificate, chain, errors, SurfaceTls.ClientAuthentication, out string why))
                {
                    return true;
                }

                refused?.Invoke(why);
                return false;
            };
        }));
    }

    // The scheme and host of a binding address. Not a Uri: Kestrel binds
    // http://*:5087 and http://+:5087, which a Uri refuses to parse.
    private static (string Scheme, string Host) Parts(string address)
    {
        int separator = address.IndexOf("://", StringComparison.Ordinal);

        if (separator < 0)
        {
            throw new InvalidOperationException(
                $"REFUSED. '{address}' is not an address to bind: http(s)://<host>:<port>");
        }

        string scheme = address[..separator];
        string rest = address[(separator + 3)..];
        int end = rest.StartsWith('[')
            ? rest.IndexOf(']', StringComparison.Ordinal) + 1
            : rest.IndexOfAny([':', '/']);

        return (scheme, end <= 0 ? rest : rest[..end]);
    }
}
