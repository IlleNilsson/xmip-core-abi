namespace Xmip.Surface;

/// <summary>
/// The platform's keeper of a person's operator key (ADR-0034, amendment
/// 2026-10-15): the key is made inside it and never leaves it, and every
/// signature is asked of it — the request's, then every handshake's. One per
/// platform, in the app that runs there: the Android Keystore (StrongBox
/// where the phone has it), the iOS keychain with the Secure Enclave, and
/// Windows CNG (the TPM through the Microsoft Platform Crypto Provider where
/// the machine has one, else the software provider, never exportable).
/// What a request names and signs and how the request is assembled are the
/// runtime's (<see cref="OperatorCertificate.RequestKept"/>); a keeper only
/// keeps and signs.
/// </summary>
public interface IOperatorKeeper
{
    /// <summary>Where the key is kept, in words a person reads: "the Android
    /// Keystore, in StrongBox".</summary>
    public string Name { get; }

    /// <summary>Whether a key is kept under <paramref name="alias"/>.</summary>
    public bool Holds(string alias);

    /// <summary>
    /// A fresh P-256 key made under <paramref name="alias"/>, its private
    /// half never exportable; answered with its public key in the form the
    /// platform hands it — <c>SubjectPublicKeyInfo</c> DER, or the bare
    /// uncompressed point — which the runtime reads either way.
    /// </summary>
    /// <exception cref="InvalidOperationException">The keeper could not make
    /// one, in its words.</exception>
    public byte[] Make(string alias);

    /// <summary>The public key of the key kept under
    /// <paramref name="alias"/>, in the form <see cref="Make"/> answers.</summary>
    /// <exception cref="InvalidOperationException">No key is kept there.</exception>
    public byte[] PublicKey(string alias);

    /// <summary>
    /// ECDSA on P-256 with SHA-256 over <paramref name="message"/>, which the
    /// keeper hashes, by the key kept under <paramref name="alias"/>; the
    /// signature DER, <c>r</c> and <c>s</c> as X.509 carries them.
    /// </summary>
    /// <exception cref="InvalidOperationException">The keeper refused or
    /// failed, in its words.</exception>
    public byte[] Sign(string alias, byte[] message);

    /// <summary>The key kept under <paramref name="alias"/> deleted; nothing
    /// where none is kept.</summary>
    public void Forget(string alias);
}
