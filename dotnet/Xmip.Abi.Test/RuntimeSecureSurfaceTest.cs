using System.Security.Cryptography.X509Certificates;
using Xmip.Abi.Operate;

namespace Xmip.Abi.Test;

/// <summary>
/// <see cref="RuntimeSecureSurface"/> crosses section 21 of
/// <c>xmip_operate.h</c> to the runtime this estate built. The handshake and
/// the issuing are tested in Rust, where they are written
/// (<c>secure_listener.rs</c>, <c>xmip-core-provision</c>'s <c>host.rs</c>);
/// a browser served through it is <c>Xmip.Surface.Test</c>'s
/// <c>BrowserCallerTest</c>. These prove the crossing: a host issued under an
/// authority made in a folder, read back as X.509 for <c>127.0.0.1</c>, a
/// listener opened on it and stopped, and a refusal carried in words.
/// </summary>
public sealed class RuntimeSecureSurfaceTest
{
    private static RuntimeSecureSurface Hosting => RuntimeRulesTest.Rules.Hosting;

    [Fact]
    public void AHostIssuedUnderAKeptAuthorityIsListenedOnAndStopped()
    {
        string place = Directory.CreateTempSubdirectory("xmip-abi-host-").FullName;

        try
        {
            string authority = Path.Combine(place, "authority");
            Assert.True(
                Hosting.Issue(authority, "test", "127.0.0.1", place, out SecureSurfaceIssued issued,
                    out string refused),
                refused);
            using X509Certificate2 leaf =
                X509CertificateLoader.LoadCertificateFromFile(issued.Certificate);
            Assert.Equal("CN=127.0.0.1", leaf.Subject);
            Assert.Equal(Path.Combine(authority, "authority.pem"), issued.Anchor);

            using SecureSurfaceListener? listener = Hosting.Open(
                Protection.Tls, "127.0.0.1:0", File.ReadAllText(issued.Certificate),
                File.ReadAllText(issued.Key), File.ReadAllText(issued.Anchor), out string reason);
            Assert.NotNull(listener);
            Assert.StartsWith("127.0.0.1:", listener.Address, StringComparison.Ordinal);
            listener.Stop();
            Assert.True(listener.Accept().Stopped, reason);

            Assert.False(Hosting.Issue(authority, "test", " ", place, out _, out refused));
            Assert.Contains("host", refused, StringComparison.Ordinal);
            Assert.Null(Hosting.Open((Protection)7, "127.0.0.1:0", "", "", "", out reason));
            Assert.Contains("no protection 7", reason, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(place, recursive: true);
        }
    }
}
