using System.Net;
using Microsoft.AspNetCore.Connections;
using Xmip.Abi.Operate;

namespace Xmip.Surface.Relay;

/// <summary>
/// A Kestrel transport whose TLS is Xmip's own (ADR-0009, amendment
/// 2026-10-21; CONTRIBUTING, <i>dogfooding</i>): every
/// <see cref="SecureEndPoint"/> is bound by the runtime's HTTPS listener
/// (<c>xmip_operate.h</c> section 21) under <paramref name="protection"/>,
/// presenting <paramref name="surface"/>'s
/// certificate and asking each browser for one — verified against its
/// anchors where presented, a browser that presents none let through as no
/// one — and Kestrel serves HTTP over what it hands over. It binds nothing
/// else (<see cref="CanBind"/>), so a plain loopback address keeps Kestrel's
/// own sockets. A certificate the anchors refuse ends the handshake in the
/// runtime and is told to <paramref name="refused"/>.
/// </summary>
/// <param name="protection">How each connection is protected:
/// <see cref="Protection.Tls"/>, the one built.</param>
/// <param name="surface">What the host presents and trusts; its files are read
/// as each address is bound.</param>
/// <param name="refused">Told each certificate the anchors refused, in the
/// runtime's words: the host's audit.</param>
public sealed class SecureTransport(
    Protection protection, SecureSurface surface, Action<string>? refused)
    : IConnectionListenerFactory, IConnectionListenerFactorySelector
{
    private readonly List<SecureEndPoint> bound = [];

    private readonly Lock gate = new();

    /// <summary>Every address bound, the port each was given.</summary>
    public IReadOnlyList<SecureEndPoint> Bound
    {
        get
        {
            lock (gate)
            {
                return [.. bound];
            }
        }
    }

    /// <inheritdoc />
    public bool CanBind(EndPoint endpoint)
    {
        return endpoint is SecureEndPoint;
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The endpoint is not
    /// Xmip's, a file is not named, or the runtime refused to listen; the
    /// message says why.</exception>
    public ValueTask<IConnectionListener> BindAsync(
        EndPoint endpoint, CancellationToken cancellationToken = default)
    {
        if (endpoint is not SecureEndPoint asked)
        {
            throw new InvalidOperationException(
                $"REFUSED. {endpoint} is not an address Xmip's TLS binds");
        }

        (string? certificate, string? key, string? anchors) = surface.Paths;

        if (certificate is null || key is null || anchors is null)
        {
            throw new InvalidOperationException(
                $"REFUSED. {asked.Url} needs {SecureSurface.CertificateKey}, "
                + $"{SecureSurface.PrivateKeyKey} and {SecureSurface.TrustAnchorKey} (ADR-0063)");
        }

        SecureSurfaceListener listener = RuntimeLibrary.Rules.Hosting.Open(
            protection, asked.Listen, File.ReadAllText(certificate), File.ReadAllText(key),
            File.ReadAllText(anchors), out string reason)
            ?? throw new InvalidOperationException($"FAILED. {asked.Url}: {reason}");
        int port = int.Parse(
            listener.Address[(listener.Address.LastIndexOf(':') + 1)..],
            System.Globalization.CultureInfo.InvariantCulture);
        SecureEndPoint given = new(asked.Host, port);

        lock (gate)
        {
            bound.Add(given);
        }

        return ValueTask.FromResult<IConnectionListener>(
            new SecureListener(listener, given, refused));
    }

    /// <summary>
    /// <paramref name="urls"/> as a browser reaches them: Kestrel names an
    /// address of a transport not its own with <c>http://</c>, so each this
    /// transport bound is named <c>https://</c> here.
    /// </summary>
    public IReadOnlyList<string> Shown(IEnumerable<string> urls)
    {
        ArgumentNullException.ThrowIfNull(urls);

        IReadOnlyList<SecureEndPoint> ours = Bound;

        return
        [
            .. urls.Select(url => ours.FirstOrDefault(
                    end => string.Equals(url, $"http://{end}", StringComparison.Ordinal))
                is { } end ? end.Url : url),
        ];
    }
}
