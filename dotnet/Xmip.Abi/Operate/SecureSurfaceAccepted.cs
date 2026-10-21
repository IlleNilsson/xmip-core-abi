namespace Xmip.Abi.Operate;

/// <summary>
/// What an <see cref="SecureSurfaceListener.Accept"/> handed over (section 21): a
/// session whose handshake finished, or why one did not, or that the
/// listener was stopped.
/// </summary>
/// <param name="Session">The session; null where none was handed over.</param>
/// <param name="Refused">Whether a browser's certificate was refused by the
/// anchors — what a host tells its audit.</param>
/// <param name="Stopped">Whether the listener was stopped, and accepts no
/// more.</param>
/// <param name="Said">Why a handshake did not finish, in the runtime's
/// words; empty otherwise.</param>
public sealed record SecureSurfaceAccepted(
    SecureSurfaceSession? Session, bool Refused, bool Stopped, string Said);
