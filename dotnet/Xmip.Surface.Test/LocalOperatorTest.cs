using System.Diagnostics;
using Xmip.Abi.Operate;

namespace Xmip.Surface.Test;

/// <summary>
/// The surface over a node on this machine (ADR-0014, amendment
/// 2026-10-13), proved against a real local endpoint — a named pipe on
/// Windows, a Unix domain socket elsewhere — served by the runtime this
/// estate built, over gRPC with no certificate: it answers what the node
/// publishes, it is told when that advances and how fast, measured end to
/// end into .NET against the millisecond rule; an act is the user this
/// process runs as and is admitted by the role the node grants; a node lost
/// is said so; and a surface that names no node says that. Run alone
/// (<see cref="Measured"/>), so the time is Xmip's and not the run's.
/// </summary>
[Collection(Measured.Name)]
public sealed class LocalOperatorTest
{
    private static readonly TestCluster Cluster = TestCluster.Read();

    [Fact]
    public async Task AnswersWhatTheNodePublishesAndIsToldWithinAMillisecond()
    {
        string published = Fixture("snapshot.toml");
        using ServedNode served = Serve(published);
        using LocalOperator localNode = new(served.Address);
        IOperatorSurface surface = localNode;
        SnapshotOperator snapshot = new(FixturePath("snapshot.toml"));

        Assert.True(localNode.Connect(), localNode.Reason);
        Assert.Equal($"LOCAL — {served.Address}", surface.Source);
        Assert.Equal(snapshot.Health(ScopeTree.Root), surface.Health(ScopeTree.Root));

        using CancellationTokenSource patience = new(TimeSpan.FromSeconds(30));
        await using IAsyncEnumerator<SurfaceChange> feed =
            surface.WatchAsync(patience.Token).GetAsyncEnumerator(patience.Token);

        Assert.True(await feed.MoveNextAsync().ConfigureAwait(true));
        Assert.True(await feed.MoveNextAsync().ConfigureAwait(true));
        ulong first = feed.Current.Revision;

        // The millisecond rule: from the node posting its next publication to
        // the notice in a .NET watch's hand, over many rounds. The notice comes
        // first; the publication is read by the question after it.
        List<double> took = [];

        for (int round = 1; round <= 200; round++)
        {
            Stopwatch posted = Stopwatch.StartNew();
            served.Post(published + $"\n# round {round}\n");
            Assert.True(await feed.MoveNextAsync().ConfigureAwait(true));
            took.Add(posted.Elapsed.TotalMilliseconds);
            Assert.Equal(first + (ulong)round, feed.Current.Revision);
        }

        took.Sort();
        double median = took[took.Count / 2];
        Console.WriteLine(
            $"a local follow told into .NET: median {median:F3} ms, "
            + $"p99 {took[took.Count * 99 / 100]:F3} ms");
        Assert.True(median < 1.0, $"median {median:F3} ms");
        Assert.Equal(snapshot.Health(ScopeTree.Root), surface.Health(ScopeTree.Root));
    }

    [Fact]
    public void AnActIsTheUsersAndIsAdmittedByTheNodesRole()
    {
        string node = $"{Cluster.Scope}/node/{Cluster.WithRole("sending")}";

        using (ServedNode observed = Serve(Fixture("snapshot.toml")))
        using (LocalOperator local = new(observed.Address))
        {
            JourneyOperation refused = local.Act(node, "j1", JourneyAct.Retry, "a name");
            Assert.False(refused.Applied);
            Assert.StartsWith("REFUSED. ", refused.Result, StringComparison.Ordinal);
            Assert.Contains(
                Environment.UserName, refused.Result, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("a name", refused.Result, StringComparison.Ordinal);
        }

        using ServedNode operated = Serve(Fixture("snapshot.toml"), "operator");
        using LocalOperator acting = new(operated.Address);
        JourneyOperation taken = acting.Act(node, "j1", JourneyAct.Retry, "a name");
        Assert.False(taken.Applied);
        Assert.Equal($"REFUSED: no node at {node} runs in this process", taken.Result);
    }

    [Fact]
    public async Task ANodeLostIsToldAndItsLastPublicationIsNotAnsweredAsCurrent()
    {
        ServedNode served = Serve(Fixture("snapshot.toml"));
        using LocalOperator local = new(served.Address);
        IOperatorSurface surface = local;
        using CancellationTokenSource patience = new(TimeSpan.FromSeconds(30));
        await using IAsyncEnumerator<SurfaceChange> feed =
            surface.WatchAsync(patience.Token).GetAsyncEnumerator(patience.Token);

        Assert.Equal($"LOCAL — {served.Address}", await Next(feed, Reached).ConfigureAwait(true));
        Assert.NotEmpty(surface.Health(ScopeTree.Root));

        Stopwatch stopped = Stopwatch.StartNew();
        served.Dispose();
        Assert.True(stopped.Elapsed < TimeSpan.FromSeconds(1), $"{stopped.Elapsed}");

        string lost = await Next(feed, source => !Reached(source)).ConfigureAwait(true);
        Assert.Contains(" unreachable: ", lost, StringComparison.Ordinal);
        Assert.Empty(surface.Health(ScopeTree.Root));
    }

    [Fact]
    public void ANodeThatIsNotThereAndNoNodeNamedAreSaidSo()
    {
        using LocalOperator absent = new($"xmip-nobody-{Guid.NewGuid():n}");
        Assert.False(absent.Connect());
        Assert.Contains(" unreachable: ", absent.Source, StringComparison.Ordinal);
        Assert.Empty(absent.Health(ScopeTree.Root));

        using LocalOperator unnamed = new(null);
        Assert.False(unnamed.Connect());
        Assert.Contains(LocalOperator.Unnamed, unnamed.Source, StringComparison.Ordinal);
        Assert.NotNull(LocalOperator.Refusal(" "));
        Assert.Null(LocalOperator.Refusal("xmip-node"));
    }

    private static string FixturePath(string name)
    {
        return Path.Combine(AppContext.BaseDirectory, "Fixture", name);
    }

    private static string Fixture(string name)
    {
        return File.ReadAllText(FixturePath(name));
    }

    /// <summary>A local endpoint no other test uses, served by the runtime
    /// this estate built, answering <paramref name="publication"/>, every
    /// caller holding <paramref name="role"/>.</summary>
    private static ServedNode Serve(string publication, string role = "")
    {
        ServedNode served = RuntimeLibrary.Rules.Remotes.ServeLocal(
            $"xmip-local-operator-test-{Guid.NewGuid():n}", role, "local-operator-test",
            out string reason)
            ?? throw new InvalidOperationException(reason);
        served.Post(publication);
        return served;
    }

    private static bool Reached(string source)
    {
        return !source.Contains(" unreachable: ", StringComparison.Ordinal);
    }

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
