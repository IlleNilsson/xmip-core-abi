using Microsoft.Extensions.Configuration;
using Xmip.Surface.Relay;

namespace Xmip.Surface.Test;

/// <summary>
/// Where a web host may listen (ADR-0063 clause 1): HTTPS anywhere with a
/// certificate to present, plain HTTP on loopback only and said so, and
/// nothing else.
/// </summary>
public sealed class SurfaceBindingTest
{
    [Theory]
    [InlineData("http://127.0.0.1:5087")]
    [InlineData("http://localhost:5087")]
    [InlineData("http://[::1]:5087")]
    public void PlainHttpOnLoopbackIsBoundAndAnswered(string address)
    {
        Assert.Equal([address], SurfaceBinding.Check([address], SecureSurface.None));
    }

    [Theory]
    [InlineData("http://0.0.0.0:5087")]
    [InlineData("http://*:5087")]
    [InlineData("http://+:5087")]
    [InlineData("http://192.0.2.1:5087")]
    public void PlainHttpBeyondThisMachineIsRefused(string address)
    {
        InvalidOperationException refused = Assert.Throws<InvalidOperationException>(
            () => SurfaceBinding.Check([address], SecureSurface.None));

        Assert.StartsWith("REFUSED.", refused.Message, StringComparison.Ordinal);
        Assert.Contains("ADR-0063 clause 1", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void HttpsWithNoCertificateIsRefused()
    {
        InvalidOperationException refused = Assert.Throws<InvalidOperationException>(
            () => SurfaceBinding.Check(["https://0.0.0.0:5443"], SecureSurface.None));

        Assert.Contains(SecureSurface.CertificateKey, refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void HttpsWithNoAnchorsToVerifyABrowserAgainstIsRefused()
    {
        using TestAuthority authority = new("anchorless");
        (string certificate, string privateKey) = authority.Server();
        SecureSurface tls = SecureSurface.Load(certificate, privateKey, null);

        InvalidOperationException refused = Assert.Throws<InvalidOperationException>(
            () => SurfaceBinding.Check(["https://127.0.0.1:5443"], tls));

        Assert.Contains(SecureSurface.TrustAnchorKey, refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void HttpsWithACertificateIsBoundAnywhereAndIsNotPlain()
    {
        using TestAuthority authority = new("binding");
        (string certificate, string privateKey) = authority.Server();
        SecureSurface tls = SecureSurface.Load(certificate, privateKey, authority.Anchor);

        Assert.Empty(SurfaceBinding.Check(["https://0.0.0.0:5443", "https://*:5443"], tls));
    }

    [Fact]
    public void TheAddressesAreTheEndpointsElseUrlsElseKestrelsDefault()
    {
        Assert.Equal(
            ["https://0.0.0.0:5443"],
            SurfaceBinding.Addresses(
                Configured(("Kestrel:Endpoints:Http:Url", "https://0.0.0.0:5443"))));
        Assert.Equal(
            ["http://127.0.0.1:1", "https://*:2"],
            SurfaceBinding.Addresses(Configured(("urls", "http://127.0.0.1:1; https://*:2"))));
        Assert.Equal(["http://localhost:5000"], SurfaceBinding.Addresses(Configured()));
    }

    [Fact]
    public void ACertificateWithoutItsKeyIsRefused()
    {
        using TestAuthority authority = new("half");
        (string certificate, _) = authority.Server();

        Assert.Throws<InvalidOperationException>(() => SecureSurface.Load(certificate, null, null));
        InvalidOperationException gone = Assert.Throws<InvalidOperationException>(
            () => SecureSurface.Load(certificate, certificate, certificate + ".gone"));
        Assert.Contains("is not there", gone.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheDocumentNamesTheFilesRelativeToItself()
    {
        using TestAuthority authority = new("document");
        (string certificate, string privateKey) = authority.Server();
        SecureSurface tls = SecureSurface.From(
            Configured(
                (SecureSurface.CertificateKey, Path.GetFileName(certificate)),
                (SecureSurface.PrivateKeyKey, Path.GetFileName(privateKey)),
                (SecureSurface.TrustAnchorKey, Path.GetFileName(authority.Anchor))),
            authority.Directory);

        Assert.Equal((certificate, privateKey, authority.Anchor), tls.Paths);
    }

    private static IConfiguration Configured(params (string Key, string Value)[] pairs)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(pairs.Select(
                pair => new KeyValuePair<string, string?>(pair.Key, pair.Value)))
            .Build();
    }
}
