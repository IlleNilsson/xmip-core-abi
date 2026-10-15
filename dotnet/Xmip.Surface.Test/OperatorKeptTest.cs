using System.Security.Cryptography;
using Xmip.Abi.Operate;

namespace Xmip.Surface.Test;

/// <summary>
/// A person's key kept by a keeper and never read (ADR-0034, amendment
/// 2026-10-15): the request made over it in two halves through the runtime,
/// signed by the authority a node's <c>[provision.operator]</c> names, the
/// chain kept once its leaf is shown to be the keeper's key, and presented to
/// a real operate listener with every handshake signed by the keeper — the
/// caller it proves is the person. The keeper here holds its key in memory,
/// as the platforms' keepers hold theirs out of reach; theirs are proved on
/// their platforms.
/// </summary>
public sealed class OperatorKeptTest
{
    private const string Person = "alice@example.org";

    private static readonly TestCluster Cluster = TestCluster.Read();

    private static RuntimeCertificates Certificates => RuntimeLibrary.Rules.Certificates;

    [Fact]
    public void AKeptKeyIsTheCallerTheOperateListenerProves()
    {
        using TestAuthority authority = new("operator-kept");
        using MemoryKeeper keeper = new();
        string chain = Path.Combine(authority.Directory, "phone", "operator.pem");
        string anchors = Path.Combine(authority.Directory, "phone", "anchor.pem");
        SurfaceTls tls = SurfaceTls.Kept(chain, anchors, keeper);

        Assert.Contains(
            "No certificate is kept", tls.KeptPem(out _, out _), StringComparison.Ordinal);

        OperatorKept asked = OperatorCertificate.RequestKept(Certificates, keeper, Person);
        Assert.True(asked.Ok, asked.Refused);
        Assert.True(asked.Made);
        OperatorKept renewed = OperatorCertificate.RequestKept(Certificates, keeper, Person);
        Assert.False(renewed.Made);

        Assert.True(
            Certificates.Sign(authority.Kept(), asked.Request, out string signed, out string no),
            no);
        Assert.Equal(string.Empty, OperatorCertificate.Keep(keeper, signed, chain));
        Assert.Contains(
            "No anchors are kept", tls.KeptPem(out _, out _), StringComparison.Ordinal);
        Assert.Equal(
            string.Empty,
            OperatorCertificate.KeepAnchors(File.ReadAllText(authority.Anchor), anchors));

        string node = $"{Cluster.Scope}/node/{Cluster.WithRole("sending")}";
        using ServedNode served = RemoteOperatorTest.Serve(
            authority, RemoteOperatorTest.Fixture("snapshot.toml"));
        using RemoteOperator phone = new(served.Address, tls);
        JourneyOperation refused = phone.Act(node, "j1", JourneyAct.Retry, "a name");

        Assert.False(refused.Applied);
        Assert.StartsWith(
            $"REFUSED. CN={Person} may not retry", refused.Result, StringComparison.Ordinal);
        Assert.True(keeper.Signed > 2, $"{keeper.Signed}");
    }

    [Fact]
    public void AChainForAnotherKeyIsNotKeptAndAKeeperThatFailsIsSaidSo()
    {
        using TestAuthority authority = new("operator-kept-refused");
        using MemoryKeeper keeper = new();
        using MemoryKeeper other = new();
        string chain = Path.Combine(authority.Directory, "operator.pem");

        OperatorKept asked = OperatorCertificate.RequestKept(Certificates, other, Person);
        Assert.True(
            Certificates.Sign(authority.Kept(), asked.Request, out string signed, out _));
        _ = keeper.Make(OperatorCertificate.KeptAlias);

        Assert.StartsWith(
            $"REFUSED: CN={Person} is not for the key", OperatorCertificate.Keep(
                keeper, signed, chain), StringComparison.Ordinal);
        Assert.False(File.Exists(chain));

        Assert.Equal(
            "REFUSED: the text holds no CERTIFICATE.",
            OperatorCertificate.KeepAnchors("nothing", chain));

        keeper.Failing = true;
        OperatorKept failed = OperatorCertificate.RequestKept(Certificates, keeper, Person);
        Assert.Equal("FAILED: the keeper is locked", failed.Refused);

        OperatorKept named = OperatorCertificate.RequestKept(Certificates, other, "CN=alice");
        Assert.StartsWith("REFUSED: ", named.Refused, StringComparison.Ordinal);
    }

    // A keeper whose key is held in memory and never handed out.
    private sealed class MemoryKeeper : IOperatorKeeper, IDisposable
    {
        private ECDsa? key;

        public string Name => "memory";

        public bool Failing { get; set; }

        public int Signed { get; private set; }

        public bool Holds(string alias) => key is not null;

        public byte[] Make(string alias)
        {
            key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            return PublicKey(alias);
        }

        public byte[] PublicKey(string alias)
        {
            return Held().ExportSubjectPublicKeyInfo();
        }

        public byte[] Sign(string alias, byte[] message)
        {
            Signed++;
            return Held().SignData(
                message, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);
        }

        public void Forget(string alias)
        {
            key?.Dispose();
            key = null;
        }

        public void Dispose() => Forget(OperatorCertificate.KeptAlias);

        private ECDsa Held()
        {
            return Failing
                ? throw new InvalidOperationException("the keeper is locked")
                : key ?? throw new InvalidOperationException("no key is kept");
        }
    }
}
