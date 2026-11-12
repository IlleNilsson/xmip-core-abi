using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Http;
using Xmip.Surface.Relay;

namespace Xmip.Surface.Test;

/// <summary>
/// One browser is one user across its connections (ADR-0009, amendment
/// 2026-11-12). A circuit's requests reach the web host over connections of
/// their own, each holding a handle of its own for the one caller; ASP.NET
/// Core 11 refuses a request of a long-lived connection whose authenticated
/// user differs from the one that opened it, compared by the
/// <see cref="ClaimTypes.NameIdentifier"/> claim where the user has one. So
/// the request's user names the caller the runtime's gate proved by that
/// claim, while the gate still takes the handle.
/// </summary>
public sealed class ProvenUserTest : IDisposable
{
    private readonly TestAuthority authority = new("proven-user");

    // What a test opened — sessions and certificates — ended with it.
    private readonly List<IDisposable> opened = [];

    public void Dispose()
    {
        opened.Reverse();
        opened.ForEach(one => one.Dispose());
        authority.Dispose();
    }

    [Fact]
    public void OneCertificatesRequestsOverTwoConnectionsAreOneUserAndAnothersAreNot()
    {
        X509Certificate2 browser = Certificate("browser");
        ClaimsPrincipal first = User(browser);
        ClaimsPrincipal next = User(browser);
        ClaimsPrincipal other = User(Certificate("other"));

        Assert.NotEqual(ProvenCaller.Held(first), ProvenCaller.Held(next));
        Assert.Equal(Identifier(first), Identifier(next));
        Assert.Equal(ProvenCaller.Who(first), Identifier(first));
        Assert.NotEqual(Identifier(first), Identifier(other));
        Assert.Null(Identifier(User(certificate: null)));
    }

    // The user the host's middleware proves for a request over a session
    // the runtime took for certificate, a session of its own each time.
    private ClaimsPrincipal User(X509Certificate2? certificate)
    {
        ProvenSession session = Opened(new ProvenSession(authority, certificate));
        DefaultHttpContext request = new();
        request.Features.Set(new ProvenConnection(session.Session.Caller));
        ProvenCaller.Prove(request);

        return request.User;
    }

    private static string? Identifier(ClaimsPrincipal user)
    {
        return user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }

    private X509Certificate2 Certificate(string name)
    {
        return Opened(ProvenSession.Presentable(authority.Client(name)));
    }

    private T Opened<T>(T one)
        where T : IDisposable
    {
        opened.Add(one);
        return one;
    }
}
