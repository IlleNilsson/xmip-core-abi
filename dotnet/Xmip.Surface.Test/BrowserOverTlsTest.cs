using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xmip.Abi.Operate;
using Xmip.Surface.Relay;

namespace Xmip.Surface.Test;

/// <summary>
/// A browser's every request is who its connection proved over Xmip's own
/// TLS (ADR-0009, amendment 2026-10-06): a plain connection and one
/// presenting no certificate prove no one, and a certificate no trusted
/// authority issued ends the handshake and is told to the host.
/// </summary>
public sealed class BrowserOverTlsTest
{
    [Fact]
    public async Task EveryRequestsUserIsWhatItsConnectionProvedOverXmipsOwnTls()
    {
        using TestAuthority authority = new("browser");
        using TestAuthority theirs = new("theirs");
        using X509Certificate2 client = ProvenSession.Presentable(authority.Client());
        using X509Certificate2 stranger = ProvenSession.Presentable(theirs.Client());
        List<string> refused = [];
        using SemaphoreSlim told = new(0);

        await using WebApplication plain = await Serve(SecureSurface.None).ConfigureAwait(true);
        await using WebApplication secured = await Serve(
            TestHost.Tls(authority, authority.Server()), said =>
            {
                lock (refused)
                {
                    refused.Add(said);
                }

                told.Release();
            }).ConfigureAwait(true);
        Uri https = new(secured.Services.GetRequiredService<SecureTransport>().Bound[0].Url);
        Assert.Equal(
            [https.AbsoluteUri.TrimEnd('/')],
            secured.Services.GetRequiredService<SecureTransport>().Shown(secured.Urls));

        using HttpClient loopback = new();
        using HttpClient proving = Browser(authority, client);
        using HttpClient watching = Browser(authority, null);
        using HttpClient refusedOne = Browser(authority, stranger);

        CancellationToken cancelled = TestContext.Current.CancellationToken;
        Assert.Equal(
            "nobody http",
            await loopback.GetStringAsync(Who(plain), cancelled).ConfigureAwait(true));
        Assert.Equal(
            $"{client.Subject} https",
            await proving.GetStringAsync(new Uri(https, "who"), cancelled).ConfigureAwait(true));
        Assert.Equal(
            "nobody https",
            await watching.GetStringAsync(new Uri(https, "who"), cancelled).ConfigureAwait(true));
        await Assert.ThrowsAsync<HttpRequestException>(
            () => refusedOne.GetStringAsync(new Uri(https, "who"), cancelled)).ConfigureAwait(true);
        Assert.True(
            await told.WaitAsync(TimeSpan.FromSeconds(5), cancelled).ConfigureAwait(true));
        Assert.Contains("handshake", Assert.Single(refused), StringComparison.Ordinal);
    }

    private static Uri Who(WebApplication host)
    {
        return new Uri(new Uri(host.Urls.First()), "who");
    }

    // A host that answers who each request proved, by the web host's own
    // middleware, and by which scheme it came.
    private static async Task<WebApplication> Serve(
        SecureSurface tls, Action<string>? refused = null)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls(
            tls.Paths.Certificate is null ? "http://127.0.0.1:0" : "https://127.0.0.1:0");
        builder.WebHost.UseSurface(Protection.Tls, tls, refused);
        builder.Logging.ClearProviders();

        WebApplication host = builder.Build();
        host.UseXmipProvenCaller();
        host.MapGet("/who", (HttpContext context) =>
            $"{ProvenCaller.Who(context.User) ?? "nobody"} {context.Request.Scheme}");
        await host.StartAsync().ConfigureAwait(false);

        return host;
    }

    // A browser that trusts the host by authority's anchor and presents
    // certificate, or none: the platform's TLS stands in for a browser's.
    private static HttpClient Browser(TestAuthority authority, X509Certificate2? certificate)
    {
        using X509Certificate2 anchor = X509CertificateLoader.LoadCertificateFromFile(
            authority.Anchor);
        byte[] trusted = anchor.RawData;
#pragma warning disable CA2000 // The client disposes its handler.
        HttpClientHandler handler = new();
#pragma warning restore CA2000

        if (certificate is not null)
        {
            handler.ClientCertificates.Add(certificate);
        }

        handler.ServerCertificateCustomValidationCallback = (_, offered, _, _) =>
        {
            using X509Chain chain = new();
            using X509Certificate2 root = X509CertificateLoader.LoadCertificate(trusted);
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            chain.ChainPolicy.CustomTrustStore.Add(root);
            return offered is not null && chain.Build(offered);
        };

        return new HttpClient(handler);
    }
}
