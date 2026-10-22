using System.Security.Cryptography.X509Certificates;
using Xmip.Abi.Operate;

namespace Xmip.Surface.Test;

/// <summary>
/// The cluster's authority and a node's certificate carried across an air
/// gap, through the runtime as every .NET surface asks (ADR-0034,
/// amendment 2026-10-22): an authority set up publishing at a plain-HTTP
/// address; a node's request made on the node, signed by the authority and
/// imported on the node; the chain verified by .NET's own chain builder
/// against the exported anchor, nothing added to any trust store; the
/// certificate revoked and listed; the authority backed up; and a file
/// that should not be taken refused in words.
/// </summary>
public sealed class AuthorityCertificateTest
{
    private static RuntimeCertificates Certificates => RuntimeLibrary.Rules.Certificates;

    [Fact]
    public void ANodeCertificateCarriedAcrossAnAirGapIsSignedImportedAndRevoked()
    {
        using TestAuthority place = new("authority");
        string authority = Path.Combine(place.Directory, "authority");
        string node = Path.Combine(place.Directory, "r1");

        AuthorityState set = AuthorityCertificate.SetUp(
            Certificates, authority, "c1", "http://ca.example:8080");
        Assert.True(set.Ok, set.Refused);
        Assert.Equal("http://ca.example:8080", set.Published);
        Assert.Equal(64, set.Fingerprint.Length);

        CarriedRequest request = AuthorityCertificate.Request(
            Certificates, "r1", ["r1.example"], node);
        Assert.True(request.Ok, request.Refused);
        Assert.Equal(["r1", "r1.example"], request.Names);

        string carried = Path.Combine(place.Directory, "r1.pem");
        CarriedSigned wrong = AuthorityCertificate.Sign(
            Certificates, authority, request.Request, ["r2.example"], carried);
        Assert.Contains("the signer expects", wrong.Refused, StringComparison.Ordinal);
        CarriedSigned signed = AuthorityCertificate.Sign(
            Certificates, authority, request.Request, request.Names, carried);
        Assert.True(signed.Ok, signed.Refused);
        Assert.Equal(signed.Chain, File.ReadAllText(carried));

        CarriedImport imported = AuthorityCertificate.Import(Certificates, carried, node);
        Assert.True(imported.Ok, imported.Refused);
        Assert.True(imported.AnchorTaken);
        Assert.Equal(set.Fingerprint, imported.AnchorFingerprint);
        Assert.Equal(TimeSpan.FromDays(60), imported.RenewAt - (imported.NotAfter.AddDays(-90)));

        string anchor = Path.Combine(place.Directory, "authority.cer");
        Assert.True(AuthorityCertificate.Export(Certificates, authority, anchor, "der").Ok);
        Assert.True(Chained(imported.Certificate, anchor), ".NET builds the chain to the anchor");

        CertificateStanding standing = AuthorityCertificate.Read(
            Certificates, imported.Certificate);
        Assert.Equal(("current", signed.Serial), (standing.Standing, standing.Serial));

        AuthorityRecord revoked = AuthorityCertificate.Revoke(
            Certificates, authority, signed.Serial, "keyCompromise");
        Assert.True(revoked.Ok, revoked.Refused);
        Assert.Equal("keyCompromise", revoked.Reason);
        AuthorityState inspected = AuthorityCertificate.Inspect(Certificates, authority);
        Assert.Equal(1, inspected.Revoked);
        Assert.Contains(inspected.Issued, one => one.Serial == signed.Serial
            && one.Standing == "revoked");

        string backup = Path.Combine(place.Directory, "backup");
        AuthorityExport copied = AuthorityCertificate.BackUp(Certificates, authority, backup);
        Assert.True(copied.Ok, copied.Refused);
        Assert.Equal(
            set.Fingerprint, AuthorityCertificate.Inspect(Certificates, backup).Fingerprint);
    }

    [Fact]
    public void AFileThatShouldNotBeTakenIsRefusedInWords()
    {
        using TestAuthority place = new("authority-refused");
        string node = Path.Combine(place.Directory, "r2");

        CarriedImport none = AuthorityCertificate.Import(
            Certificates, Path.Combine(place.Directory, "absent.pem"), node);
        Assert.StartsWith("REFUSED: ", none.Refused, StringComparison.Ordinal);
        Assert.False(AuthorityCertificate.Request(Certificates, "r2", ["not a name"], node).Ok);
        AuthorityState nothing = AuthorityCertificate.Inspect(Certificates, node);
        Assert.Contains("no authority is kept", nothing.Refused, StringComparison.Ordinal);
        AuthorityExport format = AuthorityCertificate.Export(
            Certificates, node, Path.Combine(node, "x"), "pfx");
        Assert.False(format.Ok);
    }

    // The chain .NET builds from the leaf in the file at certificate to the
    // anchor alone, trusted for this one build and added to no store.
    private static bool Chained(string certificate, string anchor)
    {
        X509Certificate2Collection chain = [];
        chain.ImportFromPemFile(certificate);
        using X509Chain building = new();
        building.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        using X509Certificate2 root = X509CertificateLoader.LoadCertificateFromFile(anchor);
        building.ChainPolicy.CustomTrustStore.Add(root);
        building.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        building.ChainPolicy.DisableCertificateDownloads = true;
        return building.Build(chain[0]);
    }
}
