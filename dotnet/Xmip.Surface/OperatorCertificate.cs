using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Xmip.Abi.Operate;

namespace Xmip.Surface;

/// <summary>
/// A person's certificate for every .NET surface (ADR-0034, amendment
/// 2026-10-15): a key and a request made into a folder, and a request signed
/// by the authority a node's <c>[provision.operator]</c> names, for
/// <c>xmip-cli certificate</c> and <c>New-XmipCertificate</c>. The key's and
/// the request's files, their names and their protection, what a request
/// names and what is signed are the runtime's (<c>xmip_operate.h</c> section
/// 19), and so is how the chain and the anchors a surface keeps are written
/// whole; what each must be before it is kept is checked here.
/// </summary>
public static class OperatorCertificate
{
    /// <summary>The name a platform's keeper holds the operator's key under
    /// (<see cref="IOperatorKeeper"/>).</summary>
    public const string KeptAlias = "xmip-operator";

    /// <summary>
    /// A fresh key and an operator's request for <paramref name="person"/>,
    /// written by the runtime into <paramref name="folder"/> as
    /// <c>operator.key</c> and <c>operator.csr</c>, each readable by the user
    /// this process runs as alone; the key never enters this process. Files
    /// already there are never overwritten: the request is refused, and
    /// nothing is written.
    /// </summary>
    public static OperatorRequested Request(
        RuntimeCertificates certificates, string person, string folder)
    {
        ArgumentNullException.ThrowIfNull(certificates);
        ArgumentNullException.ThrowIfNull(person);
        ArgumentNullException.ThrowIfNull(folder);

        return certificates.Request(
                person, folder, out string key, out string request, out string no)
            ? new OperatorRequested(person, key, request, string.Empty)
            : new OperatorRequested(person, string.Empty, string.Empty, $"REFUSED: {no}");
    }

    /// <summary>
    /// The request in the file <paramref name="request"/>, signed by the
    /// authority the node configuration <paramref name="configuration"/>
    /// names, the chain written to <paramref name="chain"/> where one is
    /// given — by the runtime, whole and synced, never over a file already
    /// there (<c>xmip_operator_keep_v1</c>). A file there before the signing
    /// is refused before anything is signed.
    /// </summary>
    public static OperatorSigned Sign(
        RuntimeCertificates certificates, string request, string configuration, string? chain)
    {
        ArgumentNullException.ThrowIfNull(certificates);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(configuration);

        if (!File.Exists(request))
        {
            return new OperatorSigned(request, chain, string.Empty, $"No file at {request}.");
        }

        if (chain is not null && File.Exists(chain))
        {
            return new OperatorSigned(
                request, chain, string.Empty,
                $"REFUSED: {chain} is there already; nothing is overwritten.");
        }

        if (!certificates.Sign(
                configuration, File.ReadAllText(request), out string signed, out string no))
        {
            return new OperatorSigned(request, chain, string.Empty, $"REFUSED: {no}");
        }

        if (chain is not null
            && !certificates.Keep(Path.GetFullPath(chain), signed, false, out no))
        {
            return new OperatorSigned(request, chain, string.Empty, $"REFUSED: {no}");
        }

        return new OperatorSigned(request, chain, signed, string.Empty);
    }

    /// <summary>
    /// An operator's request for <paramref name="person"/> over the key
    /// <paramref name="keeper"/> holds under <see cref="KeptAlias"/>, which
    /// never leaves it: made there first where none is kept, else the kept
    /// one asked again — a renewal keeps its key. The runtime writes what is
    /// signed and assembles the request; the keeper signs between
    /// (<c>xmip_operate.h</c> section 19). The request is answered as text
    /// for the person to share with whoever signs for the cluster's
    /// authority; nothing is written.
    /// </summary>
    public static OperatorKept RequestKept(
        RuntimeCertificates certificates, IOperatorKeeper keeper, string person)
    {
        ArgumentNullException.ThrowIfNull(certificates);
        ArgumentNullException.ThrowIfNull(keeper);
        ArgumentNullException.ThrowIfNull(person);

        try
        {
            bool made = !keeper.Holds(KeptAlias);
            byte[] publicKey = made ? keeper.Make(KeptAlias) : keeper.PublicKey(KeptAlias);

            if (!certificates.Information(person, publicKey, out byte[] information, out string no)
                || !certificates.Assemble(
                    information, keeper.Sign(KeptAlias, information), out string request, out no))
            {
                return new OperatorKept(person, keeper.Name, string.Empty, made, $"REFUSED: {no}");
            }

            return new OperatorKept(person, keeper.Name, request, made, string.Empty);
        }
        catch (InvalidOperationException failed)
        {
            return new OperatorKept(
                person, keeper.Name, string.Empty, false, $"FAILED: {failed.Message}");
        }
    }

    /// <summary>
    /// The chain the authority signed, <paramref name="chain"/> (PEM, leaf
    /// first), kept at <paramref name="path"/> — the file a host's
    /// <c>Certificate</c> names — once its leaf is shown to be the key
    /// <paramref name="keeper"/> holds: the keeper signs a probe and the
    /// leaf's key verifies it. A chain kept before is replaced whole, which
    /// is how a renewed certificate arrives. Empty where kept; else why not.
    /// </summary>
    public static string Keep(
        RuntimeCertificates certificates, IOperatorKeeper keeper, string chain, string path)
    {
        ArgumentNullException.ThrowIfNull(certificates);
        ArgumentNullException.ThrowIfNull(keeper);
        ArgumentNullException.ThrowIfNull(chain);
        ArgumentNullException.ThrowIfNull(path);

        try
        {
            using X509Certificate2 leaf = X509Certificate2.CreateFromPem(chain);
            using ECDsa? key = leaf.GetECDsaPublicKey();
            byte[] probe = RandomNumberGenerator.GetBytes(32);

            if (key is null
                || !key.VerifyData(
                    probe, keeper.Sign(KeptAlias, probe), HashAlgorithmName.SHA256,
                    DSASignatureFormat.Rfc3279DerSequence))
            {
                return $"REFUSED: {leaf.Subject} is not for the key {keeper.Name} holds.";
            }

            return Kept(certificates, path, chain);
        }
        catch (Exception failed) when (failed is CryptographicException or IOException
            or InvalidOperationException or UnauthorizedAccessException)
        {
            return $"REFUSED: {failed.Message}";
        }
    }

    /// <summary>
    /// The anchors a node's certificate must reach, <paramref name="anchors"/>
    /// (PEM, one certificate or more — the cluster's authority), kept at
    /// <paramref name="path"/>, the file a host's <c>TrustAnchor</c> names,
    /// replacing what was kept there. Whom to trust is the person's to
    /// check by other means, as the request's approval is. Empty where kept;
    /// else why not.
    /// </summary>
    public static string KeepAnchors(RuntimeCertificates certificates, string anchors, string path)
    {
        ArgumentNullException.ThrowIfNull(certificates);
        ArgumentNullException.ThrowIfNull(anchors);
        ArgumentNullException.ThrowIfNull(path);

        try
        {
            X509Certificate2Collection read = [];
            read.ImportFromPem(anchors);

            if (read.Count == 0)
            {
                return "REFUSED: the text holds no CERTIFICATE.";
            }

            return Kept(certificates, path, anchors);
        }
        catch (Exception failed) when (failed is CryptographicException or IOException
            or UnauthorizedAccessException)
        {
            return $"REFUSED: {failed.Message}";
        }
    }

    // The text kept whole at its file by the runtime (xmip_operator_keep_v1):
    // written beside it, synced and renamed over it, so a reader never sees
    // half of it. Empty where kept; else why not.
    private static string Kept(RuntimeCertificates certificates, string path, string text)
    {
        return certificates.Keep(Path.GetFullPath(path), text, true, out string refused)
            ? string.Empty
            : $"REFUSED: {refused}";
    }
}
