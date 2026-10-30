using Xmip.Abi.Operate;

namespace Xmip.Surface.Test;

/// <summary>
/// A person's certificate end to end (ADR-0034, amendment 2026-10-15): a key
/// and a request made into a folder, the request signed by the authority a
/// node's <c>[provision.operator]</c> names, and the certificate presented to
/// a real operate listener over Xmip's mutual TLS, where the caller it
/// proves is the person — <c>CN=&lt;person&gt;</c> — and an act is refused
/// by the Observer role the node grants (ADR-0009, note of 2026-10-12).
/// Nothing is overwritten, and a request that is not there is said so.
/// </summary>
public sealed class OperatorCertificateTest
{
    private const string Person = "alice@example.org";

    private static readonly TestCluster Cluster = TestCluster.Read();

    private static RuntimeCertificates Certificates => RuntimeLibrary.Rules.Certificates;

    [Fact]
    public void ASignedRequestIsTheCallerTheOperateListenerProves()
    {
        using TestAuthority authority = new("operator");
        string configuration = authority.Kept();
        string folder = Path.Combine(authority.Directory, "phone");

        OperatorRequested requested = OperatorCertificate.Request(Certificates, Person, folder);
        Assert.True(requested.Ok, requested.Refused);
        Assert.Equal(Path.Combine(folder, "operator.key"), requested.Key);
        Assert.True(OwnerOnly(requested.Key), $"{requested.Key} is the owner's alone");

        string chain = Path.Combine(folder, "operator.pem");
        OperatorSigned signed = OperatorCertificate.Sign(
            Certificates, requested.Request, configuration, chain);
        Assert.True(signed.Ok, signed.Refused);
        Assert.Equal(signed.Chain, File.ReadAllText(chain));

        string node = $"{Cluster.Scope}/node/{Cluster.WithRole("sending")}";
        using ServedNode served = RemoteOperatorTest.Serve(
            authority, RemoteOperatorTest.Fixture("snapshot.toml"));
        using RemoteOperator phone = new(
            served.Address, SecureSurface.Load(chain, requested.Key, authority.Anchor));
        MessageOperation refused = phone.Act(node, "j1", MessageAct.Retry, "a name");

        Assert.False(refused.Applied);
        Assert.StartsWith(
            $"REFUSED. CN={Person} may not act", refused.Result, StringComparison.Ordinal);
    }

    [Fact]
    public void NothingIsOverwrittenAndARequestNotThereIsSaidSo()
    {
        using TestAuthority authority = new("operator-refused");
        string configuration = authority.Kept();
        string folder = Path.Combine(authority.Directory, "phone");

        Assert.True(OperatorCertificate.Request(Certificates, Person, folder).Ok);
        OperatorRequested again = OperatorCertificate.Request(Certificates, Person, folder);
        Assert.False(again.Ok);
        Assert.Contains("nothing is overwritten", again.Refused, StringComparison.Ordinal);

        OperatorRequested named = OperatorCertificate.Request(
            Certificates, "CN=alice", Path.Combine(authority.Directory, "other"));
        Assert.StartsWith("REFUSED: ", named.Refused, StringComparison.Ordinal);

        string absent = Path.Combine(folder, "absent.csr");
        OperatorSigned none = OperatorCertificate.Sign(Certificates, absent, configuration, null);
        Assert.Equal($"No file at {absent}.", none.Refused);

        OperatorSigned over = OperatorCertificate.Sign(
            Certificates, Path.Combine(folder, "operator.csr"), configuration,
            configuration);
        Assert.Contains("nothing is overwritten", over.Refused, StringComparison.Ordinal);
    }

    // The runtime's private file, as this platform says it: on Unix mode
    // 0600; on Windows an access list protected from inheritance, granting
    // the user this process runs as and SYSTEM, no one else.
    private static bool OwnerOnly(string path)
    {
        return OperatingSystem.IsWindows()
            ? OwnerOnlyOnWindows(path)
            : File.GetUnixFileMode(path) == (UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static bool OwnerOnlyOnWindows(string path)
    {
        System.Security.AccessControl.FileSecurity access = new FileInfo(path).GetAccessControl();
        string[] granted = access
            .GetAccessRules(true, true, typeof(System.Security.Principal.SecurityIdentifier))
            .Cast<System.Security.AccessControl.FileSystemAccessRule>()
            .Select(rule => rule.IdentityReference.Value)
            .Order(StringComparer.Ordinal)
            .ToArray();
        string user = System.Security.Principal.WindowsIdentity.GetCurrent().User!.Value;
        string[] owner =
            new[] { user, "S-1-5-18" }.Distinct().Order(StringComparer.Ordinal).ToArray();
        return access.AreAccessRulesProtected && granted.SequenceEqual(owner);
    }
}
