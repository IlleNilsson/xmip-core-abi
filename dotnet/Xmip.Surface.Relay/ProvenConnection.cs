using Xmip.Abi.Operate;

namespace Xmip.Surface.Relay;

/// <summary>
/// Who a connection to a web host is, as the runtime's authentication gate
/// answered for it (ADR-0009, amendment 2026-11-01): the caller
/// <c>xmip_caller_proven_v1</c> (section 21) proved for the certificate the
/// browser presented and holds behind a handle, or null where it presented
/// none. A connection's feature, set by the secure transport that took the
/// session and read by <see cref="ProvenCaller"/> alone; the session holds
/// the caller and releases it when it ends. A connection that carries none
/// — Kestrel's own sockets, plain loopback — proves no one.
/// </summary>
/// <param name="Caller">The proven caller, or null.</param>
public sealed record ProvenConnection(HeldCaller? Caller);
