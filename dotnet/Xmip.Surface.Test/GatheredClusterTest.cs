using Xmip.Abi.Operate;

namespace Xmip.Surface.Test;

/// <summary>
/// A surface over a node that gathers its cluster's view — a node declaring
/// the monitoring role — follows that view and answers for the whole
/// cluster, saying what the view says of itself after where the node is
/// reached; a node that gathers none is followed as itself. Decided by the
/// node's answer to the cluster, never by a name (the design approved
/// 2026-11-12, section 3.4): both listeners here are served under random
/// names, and what the view says of itself is passed on word for word.
/// </summary>
public sealed class GatheredClusterTest
{
    private static readonly TestCluster Cluster = TestCluster.Read();

    [Fact]
    public async Task ANodeThatGathersItsClusterIsFollowedForTheWholeCluster()
    {
        string words = $"{Cluster.Name}, gathered by a monitoring node: 3 of 3 nodes reached";
        string gathered = Gathered(words);
        using ServedNode served = Serve(Fixture("snapshot.xmip"));
        served.PostCluster(gathered);
        using LocalOperator local = new(served.Address);
        IOperatorSurface surface = local;
        SnapshotOperator cluster = new(FixturePath("cluster.xmip"));

        Assert.True(local.Connect(), local.Reason);
        Assert.True(local.Gathers);
        Assert.Equal($"LOCAL — {served.Address} — {words}", surface.Source);
        Assert.Equal(cluster.Health(ScopeTree.Root), surface.Health(ScopeTree.Root));
        Assert.Equal(Cluster.Name, ClusterSurfaces.NameOf(surface));

        // Told as the gathered view advances, and read from it after.
        using CancellationTokenSource patience = new(TimeSpan.FromSeconds(30));
        await using IAsyncEnumerator<SurfaceChange> feed =
            surface.WatchAsync(patience.Token).GetAsyncEnumerator(patience.Token);

        Assert.True(await feed.MoveNextAsync().ConfigureAwait(true));
        Assert.True(await feed.MoveNextAsync().ConfigureAwait(true));
        ulong first = feed.Current.Revision;

        served.PostCluster(gathered + "\n# again\n");

        Assert.True(await feed.MoveNextAsync().ConfigureAwait(true));
        Assert.Equal(first + 1, feed.Current.Revision);
        Assert.Equal(cluster.Health(ScopeTree.Root), surface.Health(ScopeTree.Root));
        Assert.EndsWith(words, surface.Source, StringComparison.Ordinal);
    }

    [Fact]
    public void ANodeThatGathersNoneIsFollowedAsItself()
    {
        using ServedNode served = Serve(Fixture("snapshot.xmip"));
        using LocalOperator local = new(served.Address);
        IOperatorSurface surface = local;
        SnapshotOperator own = new(FixturePath("snapshot.xmip"));

        Assert.True(local.Connect(), local.Reason);
        Assert.False(local.Gathers);
        Assert.Equal($"LOCAL — {served.Address}", surface.Source);
        Assert.Equal(own.Health(ScopeTree.Root), surface.Health(ScopeTree.Root));
    }

    [Fact]
    public async Task ANodeNotAnsweringYetIsDecidedWhenItAnswers()
    {
        // A watch begun before the node answers is told it is unreachable;
        // the runtime's follow tries it again on its own loop, its answer
        // decides what is followed as it connects, and the watch is told.
        string words = $"{Cluster.Name}, gathered by a monitoring node: 3 of 3 nodes reached";
        string endpoint = $"xmip-gathered-late-{Guid.NewGuid():n}";
        using LocalOperator local = new(endpoint);
        IOperatorSurface surface = local;

        Assert.False(local.Connect());
        Assert.False(local.Gathers);

        using CancellationTokenSource patience = new(TimeSpan.FromSeconds(30));
        await using IAsyncEnumerator<SurfaceChange> feed =
            surface.WatchAsync(patience.Token).GetAsyncEnumerator(patience.Token);

        Assert.True(await feed.MoveNextAsync().ConfigureAwait(true));
        Assert.True(await feed.MoveNextAsync().ConfigureAwait(true));
        Assert.Contains("unreachable", feed.Current.Source, StringComparison.Ordinal);

        using ServedNode served = Serve(Fixture("snapshot.xmip"), endpoint);
        served.PostCluster(Gathered(words));

        do
        {
            Assert.True(await feed.MoveNextAsync().ConfigureAwait(true));
        }
        while (!local.Gathers || !local.IsConnected);

        // Told first, read by the next question: the view's words with it.
        Assert.NotEmpty(surface.Health(ScopeTree.Root));
        Assert.EndsWith(words, surface.Source, StringComparison.Ordinal);
    }

    // The cluster fixture as a gathered view, saying these words of itself.
    private static string Gathered(string words)
    {
        string text = Fixture("cluster.xmip");
        string[] lines = text.Split('\n');
        int said = Array.FindIndex(
            lines, line => line.StartsWith("source = ", StringComparison.Ordinal));
        lines[said] = $"source = \"{words}\"";
        return string.Join('\n', lines);
    }

    private static string FixturePath(string name)
    {
        return Path.Combine(AppContext.BaseDirectory, "Fixture", name);
    }

    private static string Fixture(string name)
    {
        return File.ReadAllText(FixturePath(name));
    }

    // A local listener this account observes, posting a node's own
    // publication; under a random name, so nothing is read from one.
    private static ServedNode Serve(string publication, string? endpoint = null)
    {
        using HeldCaller me = RuntimeLibrary.Rules.SecurityRoles.Process(out string refused)
            ?? throw new InvalidOperationException(refused);
        ServedNode served = RuntimeLibrary.Rules.Remotes.ServeLocal(
            endpoint ?? $"xmip-gathered-test-{Guid.NewGuid():n}", [me.Name], [], [],
            "gathered-test", out string reason) ?? throw new InvalidOperationException(reason);
        served.Post(publication);
        return served;
    }
}
