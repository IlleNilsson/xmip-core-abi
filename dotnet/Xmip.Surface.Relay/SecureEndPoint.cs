using System.Globalization;
using System.Net;

namespace Xmip.Surface.Relay;

/// <summary>
/// An address a web host serves HTTPS on in Xmip's own TLS (ADR-0009,
/// amendment 2026-10-21): what <see cref="SurfaceBinding.UseSurface"/> binds
/// for every <c>https://</c> address, and the only kind of address
/// <see cref="SecureTransport"/> binds, so a plain loopback address keeps
/// Kestrel's own sockets. Not an <see cref="IPEndPoint"/>, on purpose: a
/// transport that binds IP addresses must never take it.
/// </summary>
/// <param name="host">The host to listen on, as the runtime binds it: an IP
/// address, or <c>localhost</c>.</param>
/// <param name="port">The port; 0 asks for any, and the listener says which
/// it was given.</param>
public sealed class SecureEndPoint(string host, int port) : EndPoint
{
    /// <summary>The host it listens on.</summary>
    public string Host { get; } = host;

    /// <summary>The port it listens on.</summary>
    public int Port { get; } = port;

    /// <summary>The address as the runtime binds it, <c>host:port</c>, an
    /// IPv6 address in brackets.</summary>
    public string Listen => Host.Contains(':', StringComparison.Ordinal)
        && !Host.StartsWith('[')
        ? $"[{Host}]:{Port.ToString(CultureInfo.InvariantCulture)}"
        : $"{Host}:{Port.ToString(CultureInfo.InvariantCulture)}";

    /// <summary>The address a browser is pointed at.</summary>
    public string Url => $"https://{Listen}";

    /// <inheritdoc />
    public override string ToString()
    {
        return Listen;
    }
}
