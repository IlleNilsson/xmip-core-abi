using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Xmip.Abi.Operate;

namespace Xmip.Abi.Test;

/// <summary>
/// <see cref="RuntimeCertificates"/> crosses section 19 of
/// <c>xmip_operate.h</c> to the runtime this estate built. The purpose's
/// rules and the signing are tested in Rust, where they are written
/// (<c>xmip-core-provision</c>'s <c>purpose.rs</c> and <c>issuer.rs</c>);
/// these prove the crossing: a request made and signed by an authority kept
/// in a folder, read back here as an X.509 certificate for client
/// authentication alone naming the person, and a refusal carried as the
/// runtime's sentence.
/// </summary>
public sealed class RuntimeCertificatesTest
{
    private const string Person = "alice@example.org";

    private static RuntimeCertificates Certificates => RuntimeRulesTest.Rules.Certificates;

    [Fact]
    public void ARequestIsWrittenForAPersonAndANameThatIsNoneIsRefused()
    {
        string place = Directory.CreateTempSubdirectory("xmip-abi-request-").FullName;

        try
        {
            string folder = Path.Combine(place, "phone");
            Assert.True(
                Certificates.Request(Person, folder, out string key, out string request, out _));
            Assert.Equal(Path.Combine(folder, "operator.key"), key);
            Assert.Contains("PRIVATE KEY", File.ReadAllText(key), StringComparison.Ordinal);
            Assert.Contains(
                "CERTIFICATE REQUEST", File.ReadAllText(request), StringComparison.Ordinal);

            Assert.False(Certificates.Request(Person, folder, out _, out _, out string there));
            Assert.Contains("nothing is overwritten", there, StringComparison.Ordinal);

            string other = Path.Combine(place, "other");
            Assert.False(
                Certificates.Request("CN=alice", other, out _, out _, out string refused));
            Assert.Contains("person", refused, StringComparison.Ordinal);
            Assert.False(Directory.Exists(other) && Directory.EnumerateFiles(other).Any());
        }
        finally
        {
            Directory.Delete(place, recursive: true);
        }
    }

    [Fact]
    public void AChainIsKeptWholeReplacedWhereAskedAndElseNeverOverAFile()
    {
        string place = Directory.CreateTempSubdirectory("xmip-abi-keep-").FullName;

        try
        {
            string chain = Path.Combine(place, "phone", "operator.pem");
            Assert.True(Certificates.Keep(chain, "first", false, out string refused), refused);
            Assert.False(Certificates.Keep(chain, "over", false, out refused));
            Assert.Contains("nothing is overwritten", refused, StringComparison.Ordinal);
            Assert.Equal("first", File.ReadAllText(chain));

            Assert.True(Certificates.Keep(chain, "renewed", true, out refused), refused);
            Assert.Equal("renewed", File.ReadAllText(chain));
            Assert.Single(Directory.EnumerateFileSystemEntries(Path.GetDirectoryName(chain)!));
        }
        finally
        {
            Directory.Delete(place, recursive: true);
        }
    }

    [Fact]
    public void AKeepersTwoHalvesMakeARequestFromEitherFormOfItsPublicKey()
    {
        using ECDsa keeper = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        ECParameters held = keeper.ExportParameters(includePrivateParameters: false);
        byte[] point = [0x04, .. held.Q.X!, .. held.Q.Y!];

        foreach (byte[] publicKey in new[] { keeper.ExportSubjectPublicKeyInfo(), point })
        {
            Assert.True(
                Certificates.Information(Person, publicKey, out byte[] information, out string no),
                no);
            byte[] signature = keeper.SignData(
                information, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);
            Assert.True(
                Certificates.Assemble(information, signature, out string request, out no), no);

            CertificateRequest read = CertificateRequest.LoadSigningRequestPem(
                request, HashAlgorithmName.SHA256);
            Assert.Equal($"CN={Person}", read.SubjectName.Name);
        }

        Assert.True(Certificates.Information(Person, point, out byte[] signed, out _));
        byte[] fixedWidth = keeper.SignData(signed, HashAlgorithmName.SHA256);
        Assert.False(Certificates.Assemble(signed, fixedWidth, out _, out string refused));
        Assert.Contains("DER", refused, StringComparison.Ordinal);
        Assert.False(Certificates.Information(Person, [1, 2, 3], out _, out refused));
        Assert.False(string.IsNullOrEmpty(refused));
    }

    [Fact]
    public void ARequestIsSignedForClientAuthenticationAloneByTheAuthorityTheTableNames()
    {
        string place = Directory.CreateTempSubdirectory("xmip-abi-certificate-").FullName;

        try
        {
            string configuration = Authority(place);
            Assert.True(Certificates.Request(
                Person, Path.Combine(place, "phone"), out _, out string written, out _));
            string request = File.ReadAllText(written);

            Assert.True(
                Certificates.Sign(configuration, request, out string chain, out string refused),
                refused);

            using X509Certificate2 leaf = X509Certificate2.CreateFromPem(chain);
            Assert.Equal($"CN={Person}", leaf.Subject);
            Assert.Equal("CN=test cluster authority", leaf.Issuer);
            X509EnhancedKeyUsageExtension usage =
                leaf.Extensions.OfType<X509EnhancedKeyUsageExtension>().Single();
            Assert.Equal(
                ["1.3.6.1.5.5.7.3.2"],
                usage.EnhancedKeyUsages.Cast<Oid>().Select(oid => oid.Value));

            Assert.False(Certificates.Sign(configuration, "no request", out _, out refused));
            Assert.Contains("CERTIFICATE REQUEST", refused, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(place, recursive: true);
        }
    }

    [Fact]
    public void ANodeIsIssuedForItsNamesWithItsAnchorsAndAnUnnamedOneIsRefused()
    {
        string place = Directory.CreateTempSubdirectory("xmip-abi-node-").FullName;

        try
        {
            string authority = Path.Combine(place, "authority");
            string folder = Path.Combine(place, "identity", "node");
            Assert.True(
                Certificates.Issue(
                    authority, "test cluster authority", "node", ["node.example"], folder,
                    out NodeIssued issued, out string refused),
                refused);
            Assert.Equal(Path.Combine(folder, "node.pem"), issued.Certificate);
            Assert.Equal(Path.Combine(folder, "anchors.pem"), issued.Anchors);

            using X509Certificate2 leaf = X509Certificate2.CreateFromPemFile(
                issued.Certificate, issued.Key);
            Assert.Equal("CN=node", leaf.Subject);
            string named = leaf.Extensions.OfType<X509SubjectAlternativeNameExtension>()
                .Single().EnumerateDnsNames().Aggregate(string.Empty, (all, one) => all + one);
            Assert.Contains("node.example", named, StringComparison.Ordinal);
            Assert.Equal(
                File.ReadAllText(Path.Combine(authority, "authority.pem")),
                File.ReadAllText(issued.Anchors));

            Assert.False(Certificates.Issue(
                authority, "test cluster authority", " ", [], folder, out _, out string none));
            Assert.Contains("node", none, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(place, recursive: true);
        }
    }

    [Fact]
    public void AnActOnTheAuthorityCrossesAsJsonAndAnActThatIsNoneIsRefused()
    {
        string place = Directory.CreateTempSubdirectory("xmip-abi-authority-").FullName;

        try
        {
            string authority = Path.Combine(place, "authority");
            string arguments = System.Text.Json.JsonSerializer.Serialize(
                new Dictionary<string, string>
                {
                    ["authority"] = authority,
                    ["name"] = "test cluster authority",
                });
            Assert.True(
                Certificates.Authority("set-up", arguments, out string answer, out string refused),
                refused);
            using System.Text.Json.JsonDocument read = System.Text.Json.JsonDocument.Parse(answer);
            Assert.Equal(
                "test cluster authority", read.RootElement.GetProperty("subject").GetString());

            Assert.False(Certificates.Authority("rotate", "{}", out _, out string none));
            Assert.Contains("no act", none, StringComparison.Ordinal);
            Assert.False(Certificates.Authority("inspect", "not json", out _, out string bad));
            Assert.Contains("JSON", bad, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(place, recursive: true);
        }
    }

    // An authority made here, kept as the runtime reads one, and a node's
    // configuration naming it in [provision.operator]; its path.
    internal static string Authority(string place)
    {
        string kept = Directory.CreateDirectory(Path.Combine(place, "authority")).FullName;
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        CertificateRequest root = new(
            "CN=test cluster authority", key, HashAlgorithmName.SHA256);
        root.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        using X509Certificate2 authority = root.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(1));

        File.WriteAllText(Path.Combine(kept, "authority.pem"), authority.ExportCertificatePem());
        File.WriteAllText(Path.Combine(kept, "authority.key"), key.ExportPkcs8PrivateKeyPem());

        string configuration = Path.Combine(place, "node.toml");
        File.WriteAllText(
            configuration,
            "[service]\nname = \"n\"\ncluster_name = \"c\"\nnode_name = \"d\"\n\n"
                + "[provision.operator]\nauthority = \"authority\"\nlifetime = \"1h\"\n");
        return configuration;
    }
}
