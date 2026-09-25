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
        Assert.Equal([address], SurfaceBinding.Check([address], SurfaceTls.None));
    }

    [Theory]
    [InlineData("http://0.0.0.0:5087")]
    [InlineData("http://*:5087")]
    [InlineData("http://+:5087")]
    [InlineData("http://192.0.2.1:5087")]
    public void PlainHttpBeyondThisMachineIsRefused(string address)
    {
        InvalidOperationException refused = Assert.Throws<InvalidOperationException>(
            () => SurfaceBinding.Check([address], SurfaceTls.None));

        Assert.StartsWith("REFUSED.", refused.Message, StringComparison.Ordinal);
        Assert.Contains("ADR-0063 clause 1", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void HttpsWithNoCertificateIsRefused()
    {
        InvalidOperationException refused = Assert.Throws<InvalidOperationException>(
            () => SurfaceBinding.Check(["https://0.0.0.0:5443"], SurfaceTls.None));

        Assert.Contains(SurfaceTls.CertificateKey, refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void HttpsWithACertificateIsBoundAnywhereAndIsNotPlain()
    {
        using TestAuthority authority = new("binding");
        (string certificate, string privateKey) = authority.Server();
        SurfaceTls tls = SurfaceTls.Load(certificate, privateKey, authority.Anchor);

        Assert.Empty(SurfaceBinding.Check(["https://0.0.0.0:5443", "https://*:5443"], tls));
    }

    [Fact]
    public void TheAddressesAreTheEndpointsElseUrlsElseKestrelsDefault()
    {
        Assert.Equal(
            ["https://0.0.0.0:5443"],
            SurfaceBinding.Addresses(Configured(("Kestrel:Endpoints:Http:Url", "https://0.0.0.0:5443"))));
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

        Assert.Throws<InvalidOperationException>(() => SurfaceTls.Load(certificate, null, null));
    }

    [Fact]
    public void TheDocumentNamesTheFilesRelativeToItself()
    {
        using TestAuthority authority = new("document");
        (string certificate, string privateKey) = authority.Server();
        SurfaceTls tls = SurfaceTls.From(
            Configured(
                (SurfaceTls.CertificateKey, Path.GetFileName(certificate)),
                (SurfaceTls.PrivateKeyKey, Path.GetFileName(privateKey)),
                (SurfaceTls.TrustAnchorKey, Path.GetFileName(authority.Anchor))),
            authority.Directory);

        Assert.Equal("CN=xmip-server", tls.Certificate?.Subject);
        Assert.True(tls.Certificate?.HasPrivateKey);
        Assert.Single(tls.Anchors);
    }

    private static IConfiguration Configured(params (string Key, string Value)[] pairs)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(pairs.Select(
                pair => new KeyValuePair<string, string?>(pair.Key, pair.Value)))
            .Build();
    }
}
