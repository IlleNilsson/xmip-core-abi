using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;

namespace Xmip.Surface.Relay;

/// <summary>
/// Who a request to a web host proved, carried to what it acts through
/// (ADR-0009, amendment 2026-10-06): the remote surface over the hub and the
/// browser's circuit alike, by <see cref="GatedOperator.Proven"/>, the one
/// rule — a client certificate the handshake checked against the host's
/// anchors, else this machine's loopback, the host's own user, else no one.
/// A browser's circuit outlives the request that opened it, so the proven
/// caller rides the request's user, which Blazor hands the circuit as its
/// authentication state; nothing here is the platform's sign-in.
/// </summary>
public static class ProvenCaller
{
    /// <summary>The authentication type a proven caller's identity carries,
    /// and the only one <see cref="Who"/> reads.</summary>
    public const string AuthenticationType = "xmip-proven";

    /// <summary>Who <paramref name="connection"/> proved, or null.</summary>
    public static string? Of(ConnectionInfo? connection)
    {
        return GatedOperator.Proven(
            connection?.ClientCertificate?.Subject,
            connection?.RemoteIpAddress is { } from && IPAddress.IsLoopback(from));
    }

    /// <summary>The request's user: the proven caller, or no one. Whatever
    /// the request carried before is replaced, so nothing else names the
    /// caller.</summary>
    public static void Prove(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        string? who = Of(context.Connection);
        context.User = who is null
            ? new ClaimsPrincipal(new ClaimsIdentity())
            : new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.Name, who)], AuthenticationType));
    }

    /// <summary>The proven caller <paramref name="user"/> carries, or null.</summary>
    public static string? Who(ClaimsPrincipal? user)
    {
        return user?.Identity is { IsAuthenticated: true, AuthenticationType: AuthenticationType }
            identity
            ? identity.Name
            : null;
    }

    /// <summary>
    /// The caller of a browser's circuit, from the authentication state
    /// Blazor handed it from the request that opened it, and the role
    /// <paramref name="roles"/> grants them. A circuit whose state is not
    /// yet known is no one, and acts on nothing.
    /// </summary>
    public static RoleContext Of(RoleAssignment roles, AuthenticationStateProvider state)
    {
        ArgumentNullException.ThrowIfNull(roles);
        ArgumentNullException.ThrowIfNull(state);

        ClaimsPrincipal? user = null;

        try
        {
            Task<AuthenticationState> known = state.GetAuthenticationStateAsync();
            user = known.IsCompletedSuccessfully ? known.Result.User : null;
        }
        catch (InvalidOperationException)
        {
            // Asked before the circuit was handed a state: no one.
        }

        return roles.For(Who(user));
    }
}
