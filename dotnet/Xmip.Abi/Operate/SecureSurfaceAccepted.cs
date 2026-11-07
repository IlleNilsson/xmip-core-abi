namespace Xmip.Abi.Operate;

/// <summary>
/// What an <see cref="SecureSurfaceListener.Accept"/> handed over (section 21),
/// one of four and never two: a session whose handshake finished, a browser
/// whose certificate the anchors refused, a listener that was stopped, or a
/// handshake that did not finish for any other reason. Xmip's own fixed
/// answer, so a union: a switch over it names every case.
/// </summary>
public readonly union SecureSurfaceAccepted(
    SecureSurfaceAccepted.Accepted,
    SecureSurfaceAccepted.Refused,
    SecureSurfaceAccepted.Stopped,
    SecureSurfaceAccepted.Failed)
{
    /// <summary>A session whose handshake finished, now its holder's.</summary>
    /// <param name="Session">The session.</param>
    public sealed record Accepted(SecureSurfaceSession Session);

    /// <summary>A browser's certificate the anchors refused — what a host tells
    /// its audit.</summary>
    /// <param name="Said">Why, in the runtime's words.</param>
    public sealed record Refused(string Said);

    /// <summary>The listener was stopped, and accepts no more.</summary>
    public sealed record Stopped;

    /// <summary>A handshake that did not finish for any other reason: a probe
    /// that connects and hangs up, say. It never ends the listener.</summary>
    /// <param name="Said">Why, in the runtime's words.</param>
    public sealed record Failed(string Said);
}
