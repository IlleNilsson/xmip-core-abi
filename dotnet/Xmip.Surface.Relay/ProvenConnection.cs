namespace Xmip.Surface.Relay;

/// <summary>
/// Who a connection to a web host is, as the runtime's authentication gate
/// answered for it (ADR-0009, amendment 2026-11-01): the identity
/// <c>xmip_caller_proven_v1</c> (section 21) answered for the certificate the
/// browser presented, a distinguished name, or null where it presented none.
/// A connection's feature, set by the secure transport that took the session
/// and read by <see cref="ProvenCaller"/> alone; a connection that carries
/// none — Kestrel's own sockets, plain loopback — proves no one.
/// </summary>
/// <param name="Who">The proven caller, or null.</param>
public sealed record ProvenConnection(string? Who);
