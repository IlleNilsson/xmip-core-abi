using System.Net;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xmip.Abi.Operate;
using Xmip.Surface.Relay;

namespace Xmip.Surface.Test;

/// <summary>
/// A browser's act is the proven caller's, decided per caller by the one
/// gate (ADR-0009, amendment 2026-10-06): every request's user is what its
/// connection proved, a circuit's caller is that user and the role the
/// host's assignment grants it, and the act passes <see cref="GatedOperator"/>
/// as that caller and is audited as them. An unstated role grants nothing;
/// the Playground's tester is allowed by the fake directory its
/// configuration names, and by nothing else. A browser on this machine's
/// loopback is proven by nothing but a certificate, as one from anywhere
/// else (open problem 35).
/// </summary>
public sealed class BrowserCallerTest : IDisposable
{
    // The name a caller claims, which no act may carry.
    private const string Claimed = "claimed-by-the-caller";

    // An address that is not this machine's loopback (RFC 5737).
    private static readonly IPAddress Elsewhere = IPAddress.Parse("192.0.2.10");

    private readonly TestCluster cluster = TestCluster.Read();

    private readonly string scratch =
        Path.Combine(Path.GetTempPath(), $"xmip-browser-{Guid.NewGuid():n}");

    // What the machine's environment states, set aside so the run's own
    // statement is the only one these tests read.
    private readonly string? stated =
        Environment.GetEnvironmentVariable(RoleAssignment.EnvironmentVariable);

    public BrowserCallerTest()
    {
        Directory.CreateDirectory(scratch);
        Environment.SetEnvironmentVariable(RoleAssignment.EnvironmentVariable, null);
    }

    private string Orders => Path.Combine(scratch, "orders");

    private string Audit => Path.Combine(scratch, "audit");

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(RoleAssignment.EnvironmentVariable, stated);

        try
        {
            Directory.Delete(scratch, recursive: true);
        }
        catch (IOException)
        {
            // A file the platform still holds; the temp directory keeps it.
        }
    }

    [Fact]
    public void AnUnstatedRoleGrantsNothingAndTheTesterDirectoryAllowsTheTesterAlone()
    {
        RoleAssignment unstated = RoleAssignment.From(Configured());
        RoleAssignment tester = RoleAssignment.From(
            Configured((RoleAssignment.DirectoryKey, TesterDirectory.Kind)));
        RoleAssignment unbuilt = RoleAssignment.From(
            Configured((RoleAssignment.DirectoryKey, "no-such-kind")));
        RoleAssignment said = RoleAssignment.From(
            Configured((RoleAssignment.ConfigurationKey, "operator")));

        Assert.Equal(Role.Observer, unstated.For(GatedOperator.HostUser).Role);
        Assert.Equal(Role.Observer, unbuilt.For(GatedOperator.HostUser).Role);
        Assert.Equal(Role.Developer, tester.For(GatedOperator.HostUser).Role);
        Assert.Equal(Role.Observer, tester.For("CN=someone-else").Role);
        Assert.Equal(Role.Operator, said.For("CN=someone-else").Role);
        Assert.All([unstated, tester, said], roles =>
        {
            RoleContext nobody = roles.For(null);
            Assert.Equal(Role.Observer, nobody.Role);
            Assert.False(nobody.MayOperate());
        });
    }

    [Theory]
    [InlineData(null, Role.Observer)]
    [InlineData("operator", Role.Operator)]
    [InlineData("developer", Role.Developer)]
    [InlineData("observer", Role.Observer)]
    [InlineData("sovereign", Role.Observer)]
    public void ARoleTheRunStatesIsEveryProvenCallersAndAWordThatIsNoRoleIsObserver(
        string? said, Role expected)
    {
        RoleAssignment roles = RoleAssignment.From(
            said is null ? Configured() : Configured((RoleAssignment.ConfigurationKey, said)));

        Assert.Equal(expected, roles.For("CN=anyone").Role);
    }

    [Fact]
    public void AnAnonymousBrowserActIsRefusedWhateverTheRunStates()
    {
        RoleContext caller = Circuit(
            Configured((RoleAssignment.ConfigurationKey, "operator")),
            Request(Elsewhere, certificate: null));

        Assert.Null(caller.Who);
        Assert.Equal(Role.Observer, caller.Role);

        SubscriptionOperation done = Pause(caller);

        Assert.False(done.Applied);
        Assert.StartsWith(
            "REFUSED. Nothing proved who asks", done.Result, StringComparison.Ordinal);
        Assert.False(Directory.Exists(Orders));
        Assert.Contains("phase = \"Failure\"", Audited(), StringComparison.Ordinal);
    }

    [Fact]
    public void AnObserverIsRefusedAnActAndTheRefusalIsAuditedAsThem()
    {
        using X509Certificate2 certificate = Certificate("observer");
        RoleContext caller = Circuit(
            Configured((RoleAssignment.ConfigurationKey, "observer")),
            Request(Elsewhere, certificate));

        Assert.Equal(certificate.Subject, caller.Who);

        SubscriptionOperation done = Pause(caller);

        Assert.False(done.Applied);
        Assert.StartsWith(
            $"REFUSED. {certificate.Subject} may not", done.Result, StringComparison.Ordinal);
        Assert.False(Directory.Exists(Orders));
        Assert.Contains(
            $"\"who\" = \"{certificate.Subject}\"", Audited(), StringComparison.Ordinal);
    }

    [Fact]
    public void AnOperatorsActIsTakenAndAuditedAsThatOperator()
    {
        using X509Certificate2 certificate = Certificate("operator");
        RoleContext caller = Circuit(
            Configured((RoleAssignment.ConfigurationKey, "operator")),
            Request(Elsewhere, certificate));

        SubscriptionOperation done = Pause(caller);

        Assert.True(done.Applied, done.Result);
        string order = File.ReadAllText(
            Assert.Single(Directory.GetFiles(Orders, "*.toml", SearchOption.AllDirectories)));
        string audited = Audited();
        Assert.All([order, audited], text =>
        {
            Assert.Contains(certificate.Subject, text, StringComparison.Ordinal);
            Assert.DoesNotContain(Claimed, text, StringComparison.Ordinal);
        });
        Assert.Contains($"\"who\" = \"{certificate.Subject}\"", audited, StringComparison.Ordinal);
        Assert.Contains("\"role\" = \"Operator\"", audited, StringComparison.Ordinal);
    }

    [Fact]
    public void ABrowserOnLoopbackIsNoOneWithoutACertificateEvenToTheTesterDirectory()
    {
        // Loopback proves where a caller is, not who (open problem 35, the
        // owner, 2026-10-19): a browser on this machine presents a
        // certificate like one from anywhere else.
        RoleContext caller = Circuit(
            Configured((RoleAssignment.DirectoryKey, TesterDirectory.Kind)),
            Request(IPAddress.Loopback, certificate: null));

        Assert.Null(caller.Who);
        Assert.Equal(Role.Observer, caller.Role);
        Assert.False(Pause(caller).Applied);
    }

    [Fact]
    public void TheTestersOperatorCertificateActsWithTheTestersRoleAndNoOtherDoes()
    {
        // The owner, 2026-10-21: the tester acts with an operator certificate,
        // and the tester directory knows that certificate's subject.
        using X509Certificate2 testers = Certificate("tester");
        using X509Certificate2 another = Certificate("another");
        IConfiguration over = Configured(
            (RoleAssignment.DirectoryKey, TesterDirectory.Kind),
            (TesterDirectory.TesterKey, testers.Subject));

        RoleContext tester = Circuit(over, Request(IPAddress.Loopback, testers));
        Assert.Equal((testers.Subject, Role.Developer), (tester.Who, tester.Role));
        Assert.True(Pause(tester).Applied);

        RoleContext stranger = Circuit(over, Request(IPAddress.Loopback, another));
        Assert.Equal((another.Subject, Role.Observer), (stranger.Who, stranger.Role));
        RoleContext nobody = Circuit(over, Request(IPAddress.Loopback, certificate: null));
        Assert.Equal(((string?)null, Role.Observer), (nobody.Who, nobody.Role));
        Assert.All([stranger, nobody], caller => Assert.False(Pause(caller).Applied));
    }

    [Fact]
    public async Task EveryRequestsUserIsWhatItsConnectionProvedOverXmipsOwnTls()
    {
        using TestAuthority authority = new("browser");
        using TestAuthority theirs = new("theirs");
        using X509Certificate2 client = Presentable(authority.Client());
        using X509Certificate2 stranger = Presentable(theirs.Client());
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

        Assert.Equal("nobody http", await loopback.GetStringAsync(Who(plain)).ConfigureAwait(true));
        Assert.Equal(
            $"{client.Subject} https",
            await proving.GetStringAsync(new Uri(https, "who")).ConfigureAwait(true));
        Assert.Equal(
            "nobody https",
            await watching.GetStringAsync(new Uri(https, "who")).ConfigureAwait(true));
        await Assert.ThrowsAsync<HttpRequestException>(
            () => refusedOne.GetStringAsync(new Uri(https, "who"))).ConfigureAwait(true);
        Assert.True(await told.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(true));
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

    // A certificate and its key as a client presents them: Windows's TLS stack
    // presents no key held only in memory, which a PEM key becomes, so the
    // pair goes through PKCS#12 first.
    private static X509Certificate2 Presentable((string Certificate, string Key) pair)
    {
        using X509Certificate2 paired =
            X509Certificate2.CreateFromPemFile(pair.Certificate, pair.Key);

        return X509CertificateLoader.LoadPkcs12(paired.Export(X509ContentType.Pkcs12), null);
    }

    // A request as it reaches the web host, its user proved by the host's
    // middleware.
    private static DefaultHttpContext Request(IPAddress from, X509Certificate2? certificate)
    {
        DefaultHttpContext context = new();
        context.Connection.RemoteIpAddress = from;
        context.Connection.ClientCertificate = certificate;
        ProvenCaller.Prove(context);

        return context;
    }

    // The caller of the circuit that request opened, resolved as a screen
    // resolves it: the web host's registration, the authentication state
    // Blazor hands the circuit from the request's user.
    private static RoleContext Circuit(IConfiguration configuration, HttpContext request)
    {
        ServiceCollection services = new();
        services.AddSingleton(configuration);
        services.AddScoped<AuthenticationStateProvider, ServerAuthenticationStateProvider>();
        services.AddXmipProvenCaller();

        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope circuit = provider.CreateScope();
        ((IHostEnvironmentAuthenticationStateProvider)circuit.ServiceProvider
            .GetRequiredService<AuthenticationStateProvider>())
            .SetAuthenticationState(Task.FromResult(new AuthenticationState(request.User)));

        return circuit.ServiceProvider.GetRequiredService<RoleContext>();
    }

    // The caller asks to pause the test cluster's Subscription, through the
    // gate, claiming a name of their own.
    private SubscriptionOperation Pause(RoleContext caller)
    {
        SnapshotOperator inner = TestHost.Acting(cluster, scratch, Orders);
        GatedOperator gated = new(inner, caller, new ProgramAudit("Xmip.Surface.Test", Audit));

        return gated.Act(
            Assert.Single(gated.Subscriptions().Subscriptions), SubscriptionAct.Pause, Claimed);
    }

    private string Audited()
    {
        return File.ReadAllText(Path.Combine(Audit, "audit.toml"));
    }

    private static IConfiguration Configured(params (string Key, string Value)[] pairs)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(pairs.Select(pair =>
                new KeyValuePair<string, string?>(pair.Key, pair.Value)))
            .Build();
    }

    // A client certificate a test authority issued for name.
    private static X509Certificate2 Certificate(string name)
    {
        using TestAuthority authority = new(name);
        (string certificate, string key) = authority.Client(name);

        return X509Certificate2.CreateFromPemFile(certificate, key);
    }
}
