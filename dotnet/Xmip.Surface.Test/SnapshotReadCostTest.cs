using System.Diagnostics;
using System.Globalization;
using System.Text;
using Xmip.Abi.Operate;
using Xunit.Abstractions;

namespace Xmip.Surface.Test;

/// <summary>
/// What reading a Playground cluster's snapshot costs a view, by the
/// millisecond rule (CONTRIBUTING: over a millisecond beyond load is a
/// defect). A publication the size of a Playground cluster's on 2026-10-02 —
/// 2 MB, 11,504 records, 176 topology nodes — is read once: its first read is the load,
/// measured and reported; every view after it answers in well under a
/// millisecond, from what was read when the snapshot is unchanged, and from
/// what the follow read before it announced when it changed. On 2026-10-02 a
/// view read it again on every Playground tick, 867 ms a cluster.
/// </summary>
public sealed class SnapshotReadCostTest(ITestOutputHelper output) : IDisposable
{
    private const int Records = 11_504;

    private const int Drawn = 176;

    // A view's median, which a collection pause in the middle of a run does
    // not move. Unfollowed, each question asks the file system whether the
    // file changed, a few tens of microseconds each with the estate's other
    // tests running beside it; followed, nothing is asked but memory.
    private const double Unfollowed = 1;

    private const double Followed = 0.1;

    private readonly string path = Path.Combine(
        Path.GetTempPath(), $"xmip-snapshot-cost-{Guid.NewGuid():N}.toml");

    [Fact]
    public void AViewReadsAClusterSizedSnapshotInWellUnderAMillisecondAfterTheFirstRead()
    {
        File.WriteAllText(path, Publication(1));
        SnapshotOperator surface = new(path);

        Stopwatch first = Stopwatch.StartNew();
        Assert.Equal(Records, View(surface));
        first.Stop();
        output.WriteLine(
            $"first read, {new FileInfo(path).Length:N0} bytes and {Records:N0} records: "
            + $"{first.Elapsed.TotalMilliseconds:F1} ms");

        double view = Median(surface);
        output.WriteLine($"a view after it: {view * 1000:F1} µs");
        Assert.True(view < Unfollowed, $"a view of an unchanged snapshot took {view:F3} ms");
    }

    [Fact]
    public async Task AFollowedSnapshotIsReadOnceAsItChangesAndNeverByTheView()
    {
        File.WriteAllText(path, Publication(1));
        SnapshotOperator surface = new(path);
        using CancellationTokenSource stop = new(TimeSpan.FromSeconds(30));
        IAsyncEnumerator<SurfaceChange> follow =
            surface.WatchAsync(stop.Token).GetAsyncEnumerator(stop.Token);

        try
        {
            Assert.True(await follow.MoveNextAsync());
            Assert.Equal("round 1", surface.Run().Stress);

            // Replaced whole, as a publisher replaces it (observe::write_atomic).
            File.WriteAllText(path + ".next", Publication(2));
            File.Move(path + ".next", path, overwrite: true);
            Stopwatch announced = Stopwatch.StartNew();
            Assert.True(await follow.MoveNextAsync());
            output.WriteLine(
                $"the follow read the change and announced it in "
                + $"{announced.Elapsed.TotalMilliseconds:F1} ms");

            Stopwatch told = Stopwatch.StartNew();
            Assert.Equal("round 2", surface.Run().Stress);
            Assert.Equal(Records, View(surface));
            told.Stop();

            double view = Median(surface);
            output.WriteLine(
                $"the first view after it: {told.Elapsed.TotalMilliseconds * 1000:F1} µs; "
                + $"a view: {view * 1000:F1} µs");
            Assert.True(
                told.Elapsed.TotalMilliseconds < 1,
                $"the view after a change took {told.Elapsed.TotalMilliseconds:F3} ms");
            Assert.True(view < Followed, $"a view of a followed snapshot took {view:F3} ms");
        }
        finally
        {
            await stop.CancelAsync();
            await follow.DisposeAsync();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        File.Delete(path);
    }

    // What the topology page reads of a cluster when it renders.
    private static int View(IOperatorSurface surface)
    {
        TopologySnapshot topology = surface.Topology();
        ScopeIndex index = surface.Index();
        RunHeader run = surface.Run();
        string root = surface.Root();

        Assert.Equal(Drawn, topology.Nodes.Count);
        Assert.True(run.Said);
        Assert.NotNull(index.Worst(root));

        return surface.Health(ScopeTree.Root).Count;
    }

    private static double Median(IOperatorSurface surface)
    {
        double[] taken = new double[101];

        for (int at = 0; at < taken.Length; at++)
        {
            long start = Stopwatch.GetTimestamp();
            View(surface);
            taken[at] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        }

        Array.Sort(taken);
        return taken[taken.Length / 2];
    }

    // A publication of a Playground cluster's size and shape, over the test
    // cluster's names: its records, counts, run and topology, the round said
    // in the run's stress so a reader can tell which it read.
    private static string Publication(int round)
    {
        TestCluster cluster = TestCluster.Read();
        string[] moods = ["fine", "stressed", "done", "paused", "holding"];
        string[] stages = ["receive", "process", "send"];
        string[] nodes = [.. cluster.Nodes];
        string named = string.Join(", ", nodes.Select(node => $"\"{node}\""));
        string roles = string.Join(
            ", ", nodes.Select((node, place) => $"\"{node}={cluster.Roles[place]}\""));
        StringBuilder text = new();
        text.Append(
            CultureInfo.InvariantCulture,
            $"source = \"playground — {cluster.Scope}\"\nnode = \"{cluster.Scope}\"\n");

        for (int at = 0; at < Records; at++)
        {
            string mood = moods[at % moods.Length];
            string node = nodes[at % nodes.Length];
            text.Append(CultureInfo.InvariantCulture, $"""

                [[records]]
                scope = "{cluster.Scope}/node/{node}/{stages[at % 3]}/t{at / 6}/f{at % 2}"
                state = "{mood}"
                severity = {(mood == "fine" ? 0 : 90)}
                evidence = "contract fault — malformed content: {at % 48} of 48 rounds have failed"
                observed_unix_nanos = {1_790_926_816_719_603_400 + round}

                """);
        }

        foreach (string counted in new[] { "streams", "messages", "journeys", "bytes" })
        {
            text.Append(CultureInfo.InvariantCulture, $"""

                [[counts]]
                counted = "{counted}"
                value = {round * 1000}

                """);
        }

        text.Append(CultureInfo.InvariantCulture, $"""

            [run]
            cluster = "{cluster.Name}"
            tests = ["RoundTrip"]
            nodes = [{named}]
            roles = [{roles}]
            online = [{named}]
            stress = "round {round}"

            [topology]
            source = "drawn"
            observed_unix_nanos = {round}

            [[topology.nodes]]
            id = "cluster"
            kind = "cluster"
            scope = "{cluster.Scope}"
            state = "fine"
            origin = "configured"

            """);

        for (int at = 1; at < Drawn; at++)
        {
            string node = $"{nodes[at % nodes.Length]}-{at}";
            text.Append(CultureInfo.InvariantCulture, $"""

                [[topology.nodes]]
                id = "node/{node}"
                parent = "cluster"
                kind = "node"
                scope = "{cluster.Scope}/node/{node}"
                state = "fine"
                origin = "configured"

                """);
        }

        return text.ToString();
    }
}
