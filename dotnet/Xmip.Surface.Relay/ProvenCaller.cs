using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using Xmip.Abi.Operate;

namespace Xmip.Surface.Relay;

/// <summary>
/// Who a request to a web host proved, carried to what it acts through
/// (ADR-0009, amendments 2026-10-06 and 2026-11-01): the remote surface over
/// the hub and the browser's circuit alike, the one rule — the caller the
/// runtime's authentication gate proved for the client certificate the
/// handshake checked against the host's anchors, and holds behind a handle
/// (<see cref="ProvenConnection"/>, <c>xmip_caller_proven_v1</c>), else no
/// one, over this machine's loopback as from anywhere else (ADR-0009,
/// amendment 2026-10-21). A certificate's subject read here proves nothing,
/// and neither does a name: the handle is what the gate takes back. A
/// browser's circuit outlives the request that opened it, so the request's
/// user carries the handle, which Blazor hands the circuit as its
/// authentication state, and the circuit holds a handle of its own
/// (<see cref="HeldCaller.Hold"/>) for its life; nothing here is the
/// platform's sign-in.
/// </summary>
public static class ProvenCaller
{
    /// <summary>The authentication type a proven caller's identity carries,
    /// and the only one <see cref="Held"/> reads.</summary>
    public const string AuthenticationType = "xmip-proven";

    /// <summary>The claim the runtime's handle for the caller rides in.</summary>
    public const string HandleClaim = "xmip-caller";

    /// <summary>The caller <paramref name="context"/>'s connection proved,
    /// held by the runtime, or null.</summary>
    public static HeldCaller? Of(HttpContext? context)
    {
        HeldCaller? caller = context?.Features.Get<ProvenConnection>()?.Caller;

        return caller is { Handle: not 0 } ? caller : null;
    }

    /// <summary>The request's user: the proven caller — its name to show, the
    /// same name as who it is from one request to the next, and its handle —
    /// or no one. Whatever the request carried before is replaced, so nothing
    /// else names the caller.</summary>
    /// <remarks>Each connection holds a handle of its own, so a circuit's
    /// requests carry different handles for one caller. ASP.NET Core 11
    /// refuses a request of a long-lived connection whose authenticated user
    /// differs from the one that opened it, compared by the
    /// <see cref="ClaimTypes.NameIdentifier"/> claim where a user has one and
    /// by every claim where none does; without it a browser's circuit was
    /// refused at its second request and its page never updated. The gate
    /// still takes the handle, never this claim.</remarks>
    public static void Prove(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        HeldCaller? caller = Of(context);
        context.User = caller is null
            ? new ClaimsPrincipal(new ClaimsIdentity())
            : new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.Name, caller.Name),
                    new Claim(ClaimTypes.NameIdentifier, caller.Name),
                    new Claim(
                        HandleClaim, caller.Handle.ToString(CultureInfo.InvariantCulture)),
                ],
                AuthenticationType));
    }

    /// <summary>Who the proven caller <paramref name="user"/> carries is, to
    /// show; null for no one.</summary>
    public static string? Who(ClaimsPrincipal? user)
    {
        return Held(user) == 0 ? null : user!.Identity!.Name;
    }

    /// <summary>The runtime's handle <paramref name="user"/> carries; 0 for
    /// no one. A handle the runtime did not hand out is no one at the
    /// gate.</summary>
    public static ulong Held(ClaimsPrincipal? user)
    {
        return user?.Identity is { IsAuthenticated: true, AuthenticationType: AuthenticationType }
            && ulong.TryParse(
                user.FindFirst(HandleClaim)?.Value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out ulong handle)
            ? handle
            : 0;
    }

    /// <summary>
    /// The caller of a browser's circuit, from the authentication state
    /// Blazor handed it from the request that opened it — a handle of the
    /// circuit's own, held from the request's, released when the circuit
    /// ends — and the role <paramref name="roles"/> grants them. A circuit
    /// whose state is not yet known, or whose request's caller is no longer
    /// held, is no one, and acts on nothing.
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

        ulong handle = Held(user);
        HeldCaller? caller = handle == 0
            ? null
            : RuntimeLibrary.Rules.SecurityRoles.Hold(handle);

        return roles.For(caller);
    }
}
