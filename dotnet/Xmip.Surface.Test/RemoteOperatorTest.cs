using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Xmip.Abi.Operate;

namespace Xmip.Surface.Test;

/// <summary>
/// The surface over a node on another machine (ADR-0014, amendment
/// 2026-10-12), proved against a real operate listener on a loopback port,
/// served by the runtime this estate built, over gRPC inside Xmip's mutual
/// TLS with certificates a test authority issued: it answers what the node
/// publishes, it is told when that advances and how fast, a node that is not
/// there or is lost is said so, an act is the certificate's and is admitted
/// by the role the node grants it, and a certificate the other end's anchors
/// do not reach is refused (ADR-0063).
/// </summary>
public sealed class RemoteOperatorTest
{
    private static readonly TestCluster Cluster = TestCluster.Read();

    [Fact]
    public void SaysWhatTheNodesRunWasStartedWith()
    {
        using TestAuthority authority = new("run");
        using ServedNode served = Serve(authority, Fixture("cluster.toml"));
        using RemoteOperator remote = Remote(authority, served.Address);
        IOperatorSurface local = new SnapshotOperator(FixturePath("cluster.toml"));
        string receiver = Cluster.WithRole("receiving");
        string processor = Cluster.WithRole("processing");

        Assert.True(remote.Connect(), remote.Reason);
        Assert.Equal(
            $"RoundTrip · {Cluster.Name} · nodes {receiver}=receiving "
            + $"{processor}=processing+sending {Cluster.WithRole("sending")}=sending · "
            + $"online {receiver} · realistic",
            remote.Run().Line());
        Assert.Equal(
            ["processing", "sending"], ((IOperatorSurface)remote).Capability(processor).Roles);
        Assert.Equal(
            local.Topology().Nodes.Select(node => node.Kind),
            remote.Topology().Nodes.Select(node => node.Kind));
    }

    [Fact]
    public async Task AnswersWhatTheNodePublishesAndIsToldWhenItAdvances()
    {
        using TestAuthority authority = new("follow");
        string published = Fixture("snapshot.toml");
        using ServedNode served = Serve(authority, published);
        using RemoteOperator remoteNode = Remote(authority, served.Address);
        IOperatorSurface remote = remoteNode;
        IOperatorSurface local = new SnapshotOperator(FixturePath("snapshot.toml"));

        Assert.True(remoteNode.Connect(), remoteNode.Reason);
        Assert.Equal($"REMOTE — {served.Address}", remote.Source);
        Assert.Equal(local.Health(ScopeTree.Root), remote.Health(ScopeTree.Root));
        Assert.Equal(
            local.Figures(ScopeTree.Root) with { Observed = null },
            remote.Figures(ScopeTree.Root) with { Observed = null });

        using CancellationTokenSource patience = new(TimeSpan.FromSeconds(30));
        await using IAsyncEnumerator<SurfaceChange> feed =
            remote.WatchAsync(patience.Token).GetAsyncEnumerator(patience.Token);

        Assert.True(await feed.MoveNextAsync().ConfigureAwait(true));
        Assert.True(await feed.MoveNextAsync().ConfigureAwait(true));
        ulong first = feed.Current.Revision;

        // Told, not asked: the node posts its next publication and the
        // follow is told, measured from the post to the notice in hand.
        List<double> took = [];

        for (int round = 1; round <= 50; round++)
        {
            Stopwatch posted = Stopwatch.StartNew();
            served.Post(published + $"\n# round {round}\n");
            Assert.True(await feed.MoveNextAsync().ConfigureAwait(true));
            took.Add(posted.Elapsed.TotalMilliseconds);
            Assert.Equal(first + (ulong)round, feed.Current.Revision);
        }

        took.Sort();
        Console.WriteLine(
            $"a remote follow told over loopback: median {took[took.Count / 2]:F3} ms, "
            + $"worst {took[^1]:F3} ms");
        Assert.Equal(local.Health(ScopeTree.Root), remote.Health(ScopeTree.Root));
    }

    [Fact]
    public void ANodeThatIsNotThereIsSaidSo()
    {
        using TestAuthority authority = new("absent");
        string address = FreeAddress();
        using RemoteOperator remote = Remote(authority, address);

        Assert.False(remote.Connect());
        Assert.StartsWith($"REMOTE — {address} unreachable: ", remote.Source,
            StringComparison.Ordinal);
        Assert.Empty(remote.Health(ScopeTree.Root));
        Assert.Null(remote.Measure(ScopeTree.Root, Counted.Streams));
    }

    [Fact]
    public async Task ANodeLostIsToldAndItsLastPublicationIsNotAnsweredAsCurrent()
    {
        using TestAuthority authority = new("lost");
        ServedNode served = Serve(authority, Fixture("snapshot.toml"));
        using RemoteOperator remote = Remote(authority, served.Address);
        IOperatorSurface surface = remote;
        using CancellationTokenSource patience = new(TimeSpan.FromSeconds(30));
        await using IAsyncEnumerator<SurfaceChange> feed =
            surface.WatchAsync(patience.Token).GetAsyncEnumerator(patience.Token);

        Assert.Equal($"REMOTE — {served.Address}", await Next(feed, Reached).ConfigureAwait(true));
        Assert.NotEmpty(surface.Health(ScopeTree.Root));

        served.Dispose();

        string lost = await Next(feed, source => !Reached(source)).ConfigureAwait(true);
        Assert.Contains(" unreachable: ", lost, StringComparison.Ordinal);
        Assert.Empty(surface.Health(ScopeTree.Root));
    }

    [Fact]
    public void AnActIsTheCertificatesAndIsAdmittedByTheNodesRole()
    {
        using TestAuthority authority = new("act");
        string node = $"{Cluster.Scope}/node/{Cluster.WithRole("sending")}";

        using (ServedNode observed = Serve(authority, Fixture("snapshot.toml")))
        using (RemoteOperator remote = Remote(authority, observed.Address))
        {
            JourneyOperation refused = remote.Act(node, "j1", JourneyAct.Retry, "a name");
            Assert.False(refused.Applied);
            Assert.StartsWith("REFUSED. CN=xmip-client may not retry", refused.Result,
                StringComparison.Ordinal);
            Assert.DoesNotContain("a name", refused.Result, StringComparison.Ordinal);
        }

        using ServedNode operated = Serve(authority, Fixture("snapshot.toml"), "operator");
        using RemoteOperator acting = Remote(authority, operated.Address);
        JourneyOperation taken = acting.Act(node, "j1", JourneyAct.Retry, "a name");
        Assert.False(taken.Applied);
        Assert.Equal($"REFUSED: no node at {node} runs in this process", taken.Result);
    }

    [Fact]
    public void ACertificateEitherEndDoesNotTrustIsRefused()
    {
        using TestAuthority authority = new("node");
        using TestAuthority stranger = new("stranger");
        using ServedNode served = Serve(authority, Fixture("snapshot.toml"));

        (string certificate, string key) = stranger.Client();
        using RemoteOperator untrusted = new(
            served.Address, SecureSurface.Load(certificate, key, authority.Anchor));
        Assert.False(untrusted.Connect());

        (certificate, key) = authority.Client();
        using RemoteOperator distrusting = new(
            served.Address, SecureSurface.Load(certificate, key, stranger.Anchor));
        Assert.False(distrusting.Connect());
        Assert.Contains(" unreachable: ", distrusting.Source, StringComparison.Ordinal);
    }

    [Fact]
    public void NoCertificateAndNoOperateAddressAreRefusedInWords()
    {
        using TestAuthority authority = new("words");

        using RemoteOperator bare = new(
            "127.0.0.1:9", SecureSurface.Load(null, null, authority.Anchor));
        Assert.False(bare.Connect());
        Assert.Contains("presents a certificate", bare.Reason, StringComparison.Ordinal);

        using RemoteOperator url = Remote(authority, "https://host:5443");
        Assert.False(url.Connect());
        Assert.Contains("is no operate address", url.Reason, StringComparison.Ordinal);
        Assert.NotNull(RemoteOperator.Refusal(" "));
        Assert.Null(RemoteOperator.Refusal("node1:7401"));
    }

    private static string FixturePath(string name)
    {
        return Path.Combine(AppContext.BaseDirectory, "Fixture", name);
    }

    internal static string Fixture(string name)
    {
        return File.ReadAllText(FixturePath(name));
    }

    /// <summary>An operate listener on a loopback port, served by the runtime
    /// with the authority's server certificate, answering
    /// <paramref name="publication"/>, every proven caller holding
    /// <paramref name="role"/>.</summary>
    internal static ServedNode Serve(TestAuthority authority, string publication, string role = "")
    {
        (string certificate, string key) = authority.Server();
        ServedNode served = RuntimeLibrary.Rules.Remotes.Serve(
            "127.0.0.1:0", File.ReadAllText(certificate), File.ReadAllText(key),
            File.ReadAllText(authority.Anchor), role, "remote-operator-test",
            out string reason)
            ?? throw new InvalidOperationException(reason);
        served.Post(publication);
        return served;
    }

    private static RemoteOperator Remote(TestAuthority authority, string address)
    {
        (string certificate, string key) = authority.Client();
        return new RemoteOperator(address, SecureSurface.Load(certificate, key, authority.Anchor));
    }

    /// <summary>A loopback address nothing serves.</summary>
    private static string FreeAddress()
    {
        TcpListener probe = new(IPAddress.Loopback, 0);
        probe.Start();
        int port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return $"127.0.0.1:{port}";
    }

    private static bool Reached(string source)
    {
        return !source.Contains(" unreachable: ", StringComparison.Ordinal);
    }

    /// <summary>The source of the next notice that <paramref name="wanted"/>
    /// takes, skipping the others.</summary>
    private static async Task<string> Next(
        IAsyncEnumerator<SurfaceChange> feed, Func<string, bool> wanted)
    {
        while (await feed.MoveNextAsync().ConfigureAwait(true))
        {
            if (wanted(feed.Current.Source))
            {
                return feed.Current.Source;
            }
        }

        throw new InvalidOperationException("the feed ended");
    }
}
