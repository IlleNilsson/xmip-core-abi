namespace Xmip.Surface;

/// <summary>
/// The surface over a node on another machine, reached at its operate
/// address over gRPC inside Xmip's mutual TLS (ADR-0014, amendment
/// 2026-10-12; ADR-0063): a <see cref="ListenerOperator"/> that presents the
/// certificate its <see cref="SecureSurface"/> names and takes the node only
/// where its certificate reaches the anchors named with it. The node takes
/// every act as the subject that certificate proves. Where the key is kept
/// by the platform's keeper (<see cref="SecureSurface.Keeper"/>), the keeper
/// signs each handshake through the runtime and the key never leaves it.
/// </summary>
/// <param name="address">The node's operate address, <c>host:port</c>.</param>
/// <param name="tls">What this side presents and trusts.</param>
public sealed class RemoteOperator(string address, SecureSurface tls)
    : ListenerOperator("REMOTE", address, Opened(address, tls, out string reason), reason)
{
    /// <summary>What every surface says when <c>--remote</c> or
    /// <c>-Remote</c> names no node; null when it names one, whose address
    /// the runtime judges as it opens it.</summary>
    public static string? Refusal(string? address)
    {
        return string.IsNullOrWhiteSpace(address)
            ? "A remote needs a node's operate address, like node1:7401."
            : null;
    }

    private static Abi.Operate.RemoteNode? Opened(
        string address, SecureSurface tls, out string reason)
    {
        ArgumentNullException.ThrowIfNull(address);
        ArgumentNullException.ThrowIfNull(tls);

        // A key its platform's keeper holds: the keeper signs every
        // handshake, and the key never leaves it (ADR-0034, amendment
        // 2026-10-15).
        if (tls.Keeper is { } keeper)
        {
            if (tls.KeptPem(out string chain, out string trusted) is { } absent)
            {
                reason = absent;
                return null;
            }

            return RuntimeLibrary.Rules.Remotes.OpenKept(
                address, chain, trusted,
                message => keeper.Sign(OperatorCertificate.KeptAlias, message), out reason);
        }

        if (tls.Pem(out string certificate, out string key, out string anchors) is { } missing)
        {
            reason = missing;
            return null;
        }

        return RuntimeLibrary.Rules.Remotes.Open(
            address, certificate, key, anchors, out reason);
    }
}
