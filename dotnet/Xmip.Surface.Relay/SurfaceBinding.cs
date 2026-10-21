using System.Globalization;
using System.Net;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xmip.Abi.Operate;

namespace Xmip.Surface.Relay;

/// <summary>
/// Where a web host listens, and how (ADR-0063 clause 1): every address that
/// is not this machine is HTTPS in Xmip's own TLS with the host's
/// certificate, a client certificate is asked for and verified against the
/// host's anchors, and plain HTTP is bound on loopback only — the one
/// exception, which the host says where it binds it. The rule itself is
/// <see cref="SecureSurface.Permits(string, string, out string)"/>.
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
            : urls.Split(
                ';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    /// <summary>
    /// Refuse what may not be bound — plain HTTP beyond this machine, HTTPS
    /// with no certificate to present or no anchors to verify a browser's
    /// against — and answer the plain loopback addresses, for the host to say
    /// it binds them.
    /// </summary>
    /// <exception cref="InvalidOperationException">An address that may not be
    /// bound, with the reason.</exception>
    public static IReadOnlyList<string> Check(IEnumerable<string> addresses, SecureSurface surface)
    {
        ArgumentNullException.ThrowIfNull(addresses);
        ArgumentNullException.ThrowIfNull(surface);

        List<string> plain = [];

        foreach (string address in addresses)
        {
            (string scheme, string host, _) = Parts(address);
            bool secure = string.Equals(
                scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);

            if (secure && surface.Paths.Certificate is null)
            {
                throw new InvalidOperationException(
                    $"REFUSED. {address} is HTTPS and {SecureSurface.CertificateKey} names no "
                    + "certificate to present (ADR-0063 clause 1)");
            }

            if (secure && surface.Paths.TrustAnchor is null)
            {
                throw new InvalidOperationException(
                    $"REFUSED. {address} is HTTPS and {SecureSurface.TrustAnchorKey} names no "
                    + "anchors to verify a browser's certificate against, and Xmip trusts "
                    + "no operating system's (ADR-0063)");
            }

            if (!SecureSurface.Permits(scheme, host, out string refused))
            {
                throw new InvalidOperationException(
                    $"{refused}. Bind https:// with {SecureSurface.CertificateKey} and "
                    + SecureSurface.PrivateKeyKey);
            }

            if (!secure)
            {
                plain.Add(address);
            }
        }

        return plain;
    }

    /// <summary>
    /// Every HTTPS address this host binds is served in Xmip's own TLS by the
    /// runtime (<see cref="SecureTransport"/>, <c>xmip_operate.h</c> section
    /// 21), protected by <paramref name="protection"/>, presenting
    /// <paramref name="surface"/>'s certificate and asking the
    /// caller for one: a browser may decline, and then reads the pages; a
    /// certificate presented is verified against the anchors, and one they do
    /// not reach ends the handshake and is told to <paramref name="refused"/>.
    /// Kestrel's own HTTPS is never used. Where any address is HTTPS, every
    /// address the configuration names is bound here, a plain loopback one on
    /// Kestrel's own sockets; with none, Kestrel binds as it would. The
    /// transport is a service of the host, for
    /// <see cref="SecureTransport.Shown"/>. Who a request proved is
    /// <see cref="ProvenCaller"/>'s.
    /// </summary>
    public static IWebHostBuilder UseSurface(
        this IWebHostBuilder host,
        Protection protection,
        SecureSurface surface,
        Action<string>? refused = null)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(surface);

        SecureTransport transport = new(protection, surface, refused);

        return host
            .ConfigureServices(services =>
            {
                services.AddSingleton(transport);
                services.AddSingleton<IConnectionListenerFactory>(transport);
            })
            .ConfigureKestrel((context, kestrel) =>
            {
                IReadOnlyList<string> addresses = Addresses(context.Configuration);

                if (!addresses.Any(address => Parts(address).Scheme == Uri.UriSchemeHttps))
                {
                    return;
                }

                // Bound here, every one: Kestrel's own reading of its endpoints
                // would put its HTTPS on an https address.
                kestrel.Configure(new ConfigurationBuilder().Build());

                foreach (string address in addresses)
                {
                    Bind(kestrel, address);
                }
            });
    }

    // One address bound: HTTPS on Xmip's TLS, plain HTTP on Kestrel's sockets.
    private static void Bind(KestrelServerOptions kestrel, string address)
    {
        (string scheme, string host, int port) = Parts(address);
        string bare = host.TrimStart('[').TrimEnd(']');

        if (scheme == Uri.UriSchemeHttps)
        {
            string listened = bare is "*" or "+" ? "0.0.0.0" : bare;
            kestrel.Listen(
                new SecureEndPoint(listened, port), https => https.Protocols = HttpProtocols.Http1);
        }
        else if (string.Equals(bare, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            kestrel.ListenLocalhost(port);
        }
        else if (bare is "*" or "+")
        {
            kestrel.ListenAnyIP(port);
        }
        else
        {
            kestrel.Listen(IPAddress.Parse(bare), port);
        }
    }

    // The scheme, host and port of a binding address. Not a Uri: Kestrel binds
    // http://*:5087 and http://+:5087, which a Uri refuses to parse.
    private static (string Scheme, string Host, int Port) Parts(string address)
    {
        int separator = address.IndexOf("://", StringComparison.Ordinal);

        if (separator < 0)
        {
            throw new InvalidOperationException(
                $"REFUSED. '{address}' is not an address to bind: http(s)://<host>:<port>");
        }

        string scheme = address[..separator].ToLowerInvariant();
        string rest = address[(separator + 3)..].TrimEnd('/');
        int end = rest.StartsWith('[')
            ? rest.IndexOf(']', StringComparison.Ordinal) + 1
            : rest.IndexOfAny([':', '/']);
        string host = end <= 0 ? rest : rest[..end];
        string after = end <= 0 ? string.Empty : rest[end..].TrimStart(':');
        int port = int.TryParse(after, NumberStyles.None, CultureInfo.InvariantCulture, out int n)
            ? n
            : scheme == Uri.UriSchemeHttps ? 443 : 80;

        return (scheme, host, port);
    }
}
