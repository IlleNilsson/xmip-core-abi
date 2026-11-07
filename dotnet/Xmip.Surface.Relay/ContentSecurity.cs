using System.Buffers.Text;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;

namespace Xmip.Surface.Relay;

/// <summary>
/// The Operation Web's content security policy, the one every response of a
/// web host carries (<see cref="SurfaceRelayExtensions.UseXmipContentSecurity"/>):
/// everything from the host itself, no script but its own files and the one
/// element the page marks with this response's nonce, no inline style, never
/// framed, its base and its forms its own.
/// </summary>
public static class ContentSecurity
{
    /// <summary>The response header that carries the policy.</summary>
    public const string Header = "Content-Security-Policy";

    // HttpContext.Items' key: an object of its own, which no other
    // middleware can name.
    private static readonly object Key = new();

    /// <summary>
    /// This response's nonce: what an inline script the page writes — the
    /// import map — carries as its <c>nonce</c>, and nothing else may run
    /// inline. Empty where the policy was not set on the request.
    /// </summary>
    public static string Nonce(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Items[Key] as string ?? string.Empty;
    }

    /// <summary>The policy for a response whose nonce is
    /// <paramref name="nonce"/>.</summary>
    public static string Policy(string nonce)
    {
        ArgumentException.ThrowIfNullOrEmpty(nonce);

        return $"default-src 'self'; script-src 'self' 'nonce-{nonce}'; style-src 'self'; "
            + "frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
    }

    /// <summary>Give <paramref name="context"/> a nonce of its own and its
    /// response the policy. Written as the response starts, so an error page
    /// a handler re-executes after clearing the headers carries it too.</summary>
    internal static void Set(HttpContext context)
    {
        // 128 bits from the operating system's random source, the size CSP
        // asks a nonce to be unguessable at; only the source, no algorithm.
        // Base64url, which CSP's grammar takes, so no character of it is one
        // a page's HTML escapes.
        string nonce = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(16));

        context.Items[Key] = nonce;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[Header] = Policy(nonce);
            return Task.CompletedTask;
        });
    }
}
