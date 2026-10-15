namespace Xmip.Surface;

// SurfaceTls where this end's key is kept by its platform's keeper and
// never read (ADR-0034, amendment 2026-10-15): the chain and the anchors are
// files, the key the IOperatorKeeper's.
public sealed partial class SurfaceTls
{
    /// <summary>
    /// The chain at <paramref name="certificatePath"/>, whose key
    /// <paramref name="keeper"/> holds and signs with, and the anchors at
    /// <paramref name="trustAnchorPath"/> (ADR-0034, amendment 2026-10-15).
    /// The chain need not be there yet — it is kept once the authority has
    /// signed (<see cref="OperatorCertificate.Keep"/>) — and is read when a
    /// remote surface opens (<see cref="KeptPem"/>). What this end presents is
    /// for a node's operate listener only: a web host serves nothing with it.
    /// </summary>
    public static SurfaceTls Kept(
        string certificatePath, string? trustAnchorPath, IOperatorKeeper keeper)
    {
        ArgumentNullException.ThrowIfNull(certificatePath);
        ArgumentNullException.ThrowIfNull(keeper);

        // Neither file need be there yet: both are read as a remote opens.
        return new SurfaceTls(null, [], [], (certificatePath, null, trustAnchorPath))
        {
            Keeper = keeper,
        };
    }

    /// <summary>The keeper that holds this end's key, where a configuration
    /// named a certificate and no private key for a host that has one;
    /// null where the key is a file.</summary>
    public IOperatorKeeper? Keeper { get; private init; }

    /// <summary>
    /// The kept chain and the anchors as the PEM text
    /// <c>xmip-core-library-tls</c> takes them, the key the
    /// <see cref="Keeper"/>'s: null with both read; else what is missing, in
    /// words.
    /// </summary>
    public string? KeptPem(out string certificate, out string trustAnchor)
    {
        certificate = trustAnchor = string.Empty;

        if (Keeper is null || Paths.Certificate is null)
        {
            return $"REFUSED. No keeper holds this end's key; name {PrivateKeyKey}";
        }

        if (Paths.TrustAnchor is null)
        {
            return NoAnchors;
        }

        if (!File.Exists(Paths.Certificate))
        {
            return $"REFUSED. No certificate is kept at {Paths.Certificate} yet: make a request "
                + $"over the key {Keeper.Name} holds and keep the chain the cluster's "
                + "authority signs (ADR-0034)";
        }

        if (!File.Exists(Paths.TrustAnchor))
        {
            return $"REFUSED. No anchors are kept at {Paths.TrustAnchor} yet: keep the cluster's "
                + "authority, PEM, the one that signed the chain (ADR-0063)";
        }

        certificate = File.ReadAllText(Paths.Certificate);
        trustAnchor = File.ReadAllText(Paths.TrustAnchor);
        return null;
    }
}
