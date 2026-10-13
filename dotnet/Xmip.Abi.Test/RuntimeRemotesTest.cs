using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Xmip.Abi.Module;
using Xmip.Abi.Operate;

namespace Xmip.Abi.Test;

/// <summary>
/// <see cref="RuntimeRemotes"/> and <see cref="RuntimeSecurityRoles"/> cross
/// sections 17 and 18 to the runtime this estate built and back (ADR-0014,
/// amendment 2026-10-12): an operate listener this process serves, a
/// publication posted to it, a remote node opened over Xmip's mutual TLS with
/// certificates one test authority issued, the publication read, the follow
/// told when it advances, an act refused by role in the gate's words, and the
/// handle freed while a wait is in flight. What the plane does is the
/// runtime's and tested there; these prove the crossing.
/// </summary>
public sealed class RuntimeRemotesTest
{
    private static readonly TestCluster Cluster = TestCluster.Read();

    // The test cluster's processing node, by what it declares: the act is
    // refused by role before any node is looked for.
    private static readonly string Node =
        $"{Cluster.Scope}/node/{Cluster.WithRole("processing")}";

    private static RuntimeRules Rules => RuntimeRulesTest.Rules;

    [Fact]
    public async Task ANodeServedHereIsReadFollowedAndRefusesAnObserversAct()
    {
        using Issuer issuer = new();
        (string certificate, string key) = issuer.Issue("node", "1.3.6.1.5.5.7.3.1");
        using ServedNode served = Rules.Remotes.Serve(
            "127.0.0.1:0", certificate, key, issuer.Anchor, string.Empty, "abi-test",
            out string why) ?? throw new InvalidOperationException(why);
        served.Post("source = \"one\"\n");

        (certificate, key) = issuer.Issue("surface", "1.3.6.1.5.5.7.3.2");
        RemoteNode remote = Rules.Remotes.Open(
            served.Address, certificate, key, issuer.Anchor, out why)
            ?? throw new InvalidOperationException(why);

        Assert.Equal(XmipStatus.Ok, remote.Publication(out string text, out ulong revision));
        Assert.Equal(("source = \"one\"\n", 1ul), (text, revision));

        RemoteFollow reached = remote.Wait(0, TimeSpan.FromSeconds(5));
        Assert.True(reached.Reachable, reached.Reason);
        served.Post("source = \"two\"\n");
        Assert.Equal(2ul, remote.Wait(reached.Told, TimeSpan.FromSeconds(5)).Revision);

        Assert.Equal(
            XmipStatus.Auth,
            remote.Act(Node, "subscription", "orders", "pause", out string said));
        Assert.StartsWith("REFUSED. CN=surface may not pause", said, StringComparison.Ordinal);

        // Disposed while a wait is in flight on another thread: the wait
        // returns, and nothing crosses after the handle is freed.
        Task<RemoteFollow> waiting = Task.Run(
            () => remote.Wait(ulong.MaxValue - 1, TimeSpan.FromSeconds(30)));
        remote.Dispose();
        // Woken, not timed out: WaitAsync throws where it was not.
        _ = await waiting.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(true);
        Assert.Equal(XmipStatus.Unavailable, remote.Publication(out _, out _));
    }

    [Fact]
    public void AnAddressThatIsNoOperateAddressIsRefusedInTheRuntimesWords()
    {
        using Issuer issuer = new();
        (string certificate, string key) = issuer.Issue("surface", "1.3.6.1.5.5.7.3.2");

        Assert.Null(Rules.Remotes.Open(
            "https://node1:7401", certificate, key, issuer.Anchor, out string why));
        Assert.Contains("is no operate address", why, StringComparison.Ordinal);
    }

    [Fact]
    public void TheGateAndTheRoleWordsAreTheRuntimes()
    {
        Assert.Equal(1, Rules.SecurityRoles.Named(" Operator "));
        Assert.Equal(0, Rules.SecurityRoles.Named("admin"));
        Assert.Equal(("Developer", "also builds"), Rules.SecurityRoles.Words(2));
        Assert.True(Rules.SecurityRoles.Admitted("CN=a", 1, "pause", "x", out _));
        Assert.False(Rules.SecurityRoles.Admitted(null, 2, "pause", "x", out string refused));
        Assert.StartsWith("REFUSED. Nothing proved who asks to pause x", refused,
            StringComparison.Ordinal);
    }

    /// <summary>A test authority, in memory: its anchor as PEM, and a leaf
    /// for 127.0.0.1 and localhost issued for one purpose.</summary>
    private sealed class Issuer : IDisposable
    {
        private readonly ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        private readonly X509Certificate2 anchor;

        public Issuer()
        {
            CertificateRequest request = new(
                "CN=Xmip abi test anchor", key, HashAlgorithmName.SHA256);
            request.CertificateExtensions.Add(
                new X509BasicConstraintsExtension(true, false, 0, true));
            request.CertificateExtensions.Add(new X509KeyUsageExtension(
                X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
            request.CertificateExtensions.Add(
                new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
            anchor = request.CreateSelfSigned(
                DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow.AddDays(1));
            Anchor = anchor.ExportCertificatePem();
        }

        public string Anchor { get; }

        public (string Certificate, string Key) Issue(string name, string purpose)
        {
            using ECDsa leafKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            CertificateRequest request = new($"CN={name}", leafKey, HashAlgorithmName.SHA256);
            SubjectAlternativeNameBuilder names = new();
            names.AddIpAddress(IPAddress.Loopback);
            names.AddDnsName("localhost");
            request.CertificateExtensions.Add(names.Build());
            request.CertificateExtensions.Add(
                new X509BasicConstraintsExtension(false, false, 0, true));
            request.CertificateExtensions.Add(
                new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
            request.CertificateExtensions.Add(
                new X509EnhancedKeyUsageExtension([new Oid(purpose)], false));
            request.CertificateExtensions.Add(
                X509AuthorityKeyIdentifierExtension.CreateFromCertificate(anchor, true, false));
            using X509Certificate2 issued = request.Create(
                anchor,
                DateTimeOffset.UtcNow.AddMinutes(-30),
                DateTimeOffset.UtcNow.AddHours(12),
                RandomNumberGenerator.GetBytes(16));

            return (issued.ExportCertificatePem(), leafKey.ExportPkcs8PrivateKeyPem());
        }

        public void Dispose()
        {
            anchor.Dispose();
            key.Dispose();
        }
    }
}
