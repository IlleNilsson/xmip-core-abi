using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xmip.Abi.Operate;
using Xmip.Surface.Relay;

namespace Xmip.Surface.Test;

/// <summary>
/// A browser's act is the proven caller's, decided per caller by the one
/// gate (ADR-0009, amendments 2026-10-06 and 2026-11-01): every request's
/// user is the caller its connection proved, which the runtime holds behind
/// a handle; a circuit holds a handle of its own and the role the host's
/// assignment grants it; and the act passes <see cref="GatedOperator"/> by
/// that handle, never a name, and is audited as them. An unstated role
/// grants nothing; the Essentials' tester is allowed by the fake directory
/// its configuration names, and by nothing else. A browser on this
/// machine's loopback is proven by nothing but a certificate, as one from
/// anywhere else (open problem 35).
/// </summary>
public sealed class BrowserCallerTest : IDisposable
{
    // The name a caller claims, which no act may carry.
    private const string Claimed = "claimed-by-the-caller";

    // An address that is not this machine's loopback (RFC 5737).
    private static readonly IPAddress Elsewhere = IPAddress.Parse("192.0.2.10");

    private readonly TestCluster cluster = TestCluster.Read();

    private readonly TestAuthority authority = new("browser-caller");

    // What a test opened — sessions, certificates, circuits — ended with it.
    private readonly List<IDisposable> opened = [];

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

    private string Audit => Path.Combine(scratch, "audit");

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(RoleAssignment.EnvironmentVariable, stated);
        opened.Reverse();
        opened.ForEach(one => one.Dispose());
        authority.Dispose();

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

        // The desktop's caller: this process's account, as the runtime read
        // it from the operating system and proved it.
        Assert.Equal(Role.Observer, Kept(unstated.For(RoleAssignment.ProcessCaller())).Role);
        Assert.Equal(Role.Observer, Kept(unbuilt.For(RoleAssignment.ProcessCaller())).Role);
        RoleContext desktop = Kept(tester.For(RoleAssignment.ProcessCaller()));
        Assert.Equal(Role.Developer, desktop.Role);
        Assert.False(string.IsNullOrEmpty(desktop.Who));

        HeldCaller someone = Held("someone-else");
        Assert.Equal(Role.Observer, Kept(tester.For(someone.Hold())).Role);
        Assert.Equal(Role.Operator, Kept(said.For(someone.Hold())).Role);
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

        Assert.Equal(expected, Kept(roles.For(Held("anyone").Hold())).Role);
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
        Assert.Contains("phase = \"Failure\"", Audited(), StringComparison.Ordinal);
    }

    [Fact]
    public void AnObserverIsRefusedAnActAndTheRefusalIsAuditedAsThem()
    {
        X509Certificate2 certificate = Certificate("observer");
        RoleContext caller = Circuit(
            Configured((RoleAssignment.ConfigurationKey, "observer")),
            Request(Elsewhere, certificate));

        Assert.Equal(certificate.Subject, caller.Who);

        SubscriptionOperation done = Pause(caller);

        Assert.False(done.Applied);
        Assert.StartsWith(
            $"REFUSED. {certificate.Subject} may not", done.Result, StringComparison.Ordinal);
        Assert.Contains(
            $"\"who\" = \"{certificate.Subject}\"", Audited(), StringComparison.Ordinal);
    }

    [Fact]
    public void AnOperatorsActIsTakenAndAuditedAsThatOperator()
    {
        X509Certificate2 certificate = Certificate("operator");
        RoleContext caller = Circuit(
            Configured((RoleAssignment.ConfigurationKey, "operator")),
            Request(Elsewhere, certificate));

        SubscriptionOperation done = Pause(caller);

        // Admitted, it reaches the surface beneath the gate - a snapshot,
        // which declines it in its own words - as that operator.
        Assert.Equal(SnapshotOperator.NotActed, done.Result);
        string audited = Audited();
        Assert.Contains($"{certificate.Subject} asked to pause", audited, StringComparison.Ordinal);
        Assert.DoesNotContain(Claimed, audited, StringComparison.Ordinal);
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
        Assert.False(Passes(caller));
    }

    [Fact]
    public void ACertificateTheRuntimesGateDidNotAnswerForProvesNoOne()
    {
        // ADR-0009, amendment 2026-11-01: a subject the host reads off a
        // connection is no caller; only a caller xmip_caller_proven_v1 holds
        // is (GatedOperator.Proven is deleted).
        X509Certificate2 certificate = Certificate("operator");
        DefaultHttpContext request = new();
        request.Connection.ClientCertificate = certificate;
        ProvenCaller.Prove(request);

        RoleContext caller = Circuit(
            Configured((RoleAssignment.ConfigurationKey, "operator")), request);

        Assert.Null(ProvenCaller.Who(request.User));
        Assert.Equal((null, Role.Observer), (caller.Who, caller.Role));
        Assert.False(Passes(caller));
    }

    [Fact]
    public void AHandleTheRuntimeDidNotHandOutIsNoOneWhateverNameRidesWithIt()
    {
        // The gap ADR-0009's amendment of 2026-11-01 left: a host passing a
        // name the runtime never answered was believed. A forged handle,
        // with any name beside it, is no one.
        HeldCaller real = Held("operator");
        DefaultHttpContext forged = new()
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.Name, real.Name),
                    new Claim(
                        ProvenCaller.HandleClaim,
                        (real.Handle ^ 1).ToString(CultureInfo.InvariantCulture)),
                ],
                ProvenCaller.AuthenticationType)),
        };

        RoleContext caller = Circuit(
            Configured((RoleAssignment.ConfigurationKey, "operator")), forged);

        Assert.Equal((null, Role.Observer), (caller.Who, caller.Role));
        Assert.False(Passes(caller));
        Assert.False(RuntimeLibrary.Rules.SecurityRoles.Admitted(
            null, (int)Role.Developer, "pause", "x", out string refused));
        Assert.StartsWith("REFUSED. Nothing proved", refused, StringComparison.Ordinal);
    }

    [Fact]
    public void ACircuitOutlivesItsSessionAndEndsWithItselfOrItsListener()
    {
        IConfiguration operators = Configured((RoleAssignment.ConfigurationKey, "operator"));
        ProvenSession session = Opened(new ProvenSession(authority, Certificate("circuit")));
        DefaultHttpContext request = new();
        request.Features.Set(new ProvenConnection(session.Session.Caller));
        ProvenCaller.Prove(request);

        // The circuit holds its own: the connection that opened it ends.
        (RoleContext circuit, IServiceScope scope) = Scoped(operators, request);
        session.Session.Dispose();
        Assert.Equal(0ul, session.Session.Caller!.Handle);
        Assert.True(Passes(circuit), "the circuit outlives its session");

        // The circuit ends: its handle is no one.
        HeldCaller kept = circuit.Caller!;
        ulong handle = kept.Handle;
        scope.Dispose();
        Assert.Equal(0ul, kept.Handle);
        Assert.Null(RuntimeLibrary.Rules.SecurityRoles.Hold(handle));

        // The listener stops: every circuit its sessions proved is no one.
        ProvenSession another = Opened(new ProvenSession(authority, Certificate("stopped")));
        DefaultHttpContext second = new();
        second.Features.Set(new ProvenConnection(another.Session.Caller));
        ProvenCaller.Prove(second);
        RoleContext stopped = Circuit(operators, second);
        another.Stop();
        Assert.False(Passes(stopped));
    }

    [Fact]
    public void TheTestersOperatorCertificateActsWithTheTestersRoleAndNoOtherDoes()
    {
        // The owner, 2026-10-21: the tester acts with an operator certificate,
        // and the tester directory knows that certificate's subject.
        X509Certificate2 testers = Certificate("tester");
        X509Certificate2 another = Certificate("another");
        IConfiguration over = Configured(
            (RoleAssignment.DirectoryKey, TesterDirectory.Kind),
            (TesterDirectory.TesterKey, testers.Subject));

        RoleContext tester = Circuit(over, Request(IPAddress.Loopback, testers));
        Assert.Equal((testers.Subject, Role.Developer), (tester.Who, tester.Role));
        Assert.True(Passes(tester));

        RoleContext stranger = Circuit(over, Request(IPAddress.Loopback, another));
        Assert.Equal((another.Subject, Role.Observer), (stranger.Who, stranger.Role));
        RoleContext nobody = Circuit(over, Request(IPAddress.Loopback, certificate: null));
        Assert.Equal((null, Role.Observer), (nobody.Who, nobody.Role));
        Assert.All([stranger, nobody], caller => Assert.False(Passes(caller)));
    }

    // A request as it reaches the web host over a session the runtime took,
    // its connection carrying the caller the runtime's gate proved for the
    // certificate, and its user proved by the host's middleware.
    private DefaultHttpContext Request(IPAddress from, X509Certificate2? certificate)
    {
        ProvenSession session = Opened(new ProvenSession(authority, certificate));
        DefaultHttpContext context = new();
        context.Connection.RemoteIpAddress = from;
        context.Connection.ClientCertificate = certificate;
        context.Features.Set(new ProvenConnection(session.Session.Caller));
        ProvenCaller.Prove(context);

        return context;
    }

    // A caller the runtime proved for a certificate called name, held for
    // the test's life.
    private HeldCaller Held(string name)
    {
        return Opened(new ProvenSession(authority, Certificate(name))).Session.Caller
            ?? throw new InvalidOperationException("the runtime proved no one");
    }

    // The caller of the circuit that request opened, resolved as a screen
    // resolves it: the web host's registration, the authentication state
    // Blazor hands the circuit from the request's user; the circuit lasts
    // the test.
    private RoleContext Circuit(IConfiguration configuration, HttpContext request)
    {
        (RoleContext caller, IServiceScope circuit) = Scoped(configuration, request);
        Opened(circuit);

        return caller;
    }

    private (RoleContext Caller, IServiceScope Circuit) Scoped(
        IConfiguration configuration, HttpContext request)
    {
        ServiceCollection services = new();
        services.AddSingleton(configuration);
        services.AddScoped<AuthenticationStateProvider, ServerAuthenticationStateProvider>();
        services.AddXmipProvenCaller();

        ServiceProvider provider = Opened(services.BuildServiceProvider());
        IServiceScope circuit = provider.CreateScope();
        ((IHostEnvironmentAuthenticationStateProvider)circuit.ServiceProvider
            .GetRequiredService<AuthenticationStateProvider>())
            .SetAuthenticationState(Task.FromResult(new AuthenticationState(request.User)));

        return (circuit.ServiceProvider.GetRequiredService<RoleContext>(), circuit);
    }

    // The caller asks to pause the test cluster's Subscription, through the
    // gate, claiming a name of their own.
    private SubscriptionOperation Pause(RoleContext caller)
    {
        SnapshotOperator inner = TestHost.Acting(cluster, scratch);
        GatedOperator gated = new(inner, caller, new ProgramAudit("Xmip.Surface.Test", Audit));

        return gated.Act(
            Assert.Single(gated.Subscriptions().Subscriptions),
            SubscriptionAct.Pause,
            Claimed,
            caller: null);
    }

    // Whether the gate lets the caller's pause through to the surface
    // beneath it: a snapshot, which answers every act it is passed in its
    // own words; the gate's refusal is its own.
    private bool Passes(RoleContext caller)
    {
        return Pause(caller).Result == SnapshotOperator.NotActed;
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

    // A client certificate the test authority issued for name, as a client
    // presents it.
    private X509Certificate2 Certificate(string name)
    {
        return Opened(ProvenSession.Presentable(authority.Client(name)));
    }

    private RoleContext Kept(RoleContext context)
    {
        return Opened(context);
    }

    private T Opened<T>(T one)
        where T : IDisposable
    {
        opened.Add(one);
        return one;
    }
}
