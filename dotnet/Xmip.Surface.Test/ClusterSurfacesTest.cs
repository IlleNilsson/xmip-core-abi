using Microsoft.Extensions.Configuration;
using Xmip.Abi.Operate;

namespace Xmip.Surface.Test;

/// <summary>
/// One face over more than one cluster (ADR-0052, amendment 2026-09-20). Each
/// cluster keeps its own surface and its own scope tree; the set names them,
/// hands out the one asked for, refuses two that claim one cluster, and keeps
/// the name of a roll that ended.
/// </summary>
public sealed class ClusterSurfacesTest
{
    private static readonly TestCluster Cluster = TestCluster.Read();

    /// <summary>The test cluster's name, which the cluster fixture publishes.</summary>
    private static readonly string First = Cluster.Name;

    /// <summary>The second fixture's cluster, read from what it publishes.</summary>
    private static readonly string Second =
        new SnapshotOperator(Fixture("cluster-c2.toml")).Run().Cluster;

    private static string Fixture(string name)
    {
        return Path.Combine(AppContext.BaseDirectory, "Fixture", name);
    }

    /// <summary>The second fixture is the second test cluster's (ADR-0056,
    /// amendment 2026-10-03): its <c>[run]</c> names the cluster
    /// <see cref="TestCluster.ReadOther"/> reads and only nodes that cluster
    /// configures.</summary>
    [Fact]
    public void TheSecondFixtureIsTheSecondTestClustersOwn()
    {
        TestCluster other = TestCluster.ReadOther();
        RunHeader run = new SnapshotOperator(Fixture("cluster-c2.toml")).Run();

        Assert.Equal(other.Name, run.Cluster);
        Assert.NotEmpty(run.Nodes);
        Assert.All(run.Nodes, node => Assert.Contains(node, other.Nodes));
    }

    [Fact]
    public void TwoSnapshotsAreTwoClustersEachWithItsOwnTree()
    {
        using ClusterSurfaces held = ClusterSurfaces.Over(
            [new SnapshotOperator(Fixture("cluster.toml")),
             new SnapshotOperator(Fixture("cluster-c2.toml"))]);

        Assert.NotEqual(First, Second);
        Assert.Equal([First, Second], held.Clusters);
        Assert.True(held.Several(includingHidden: false));
        Assert.Equal(2, held.Count);
        Assert.Equal($"2 clusters — {First}, {Second}", held.Source);

        // Each answers its own root and nothing else: a cluster is a whole
        // scope tree, and neither can see into the other.
        Assert.Contains(
            held.For(First, includingHidden: false).Health(ScopeTree.Root),
            record => record.Scope.StartsWith($"{Cluster.Scope}/", StringComparison.Ordinal));
        Assert.DoesNotContain(
            held.For(First, includingHidden: false).Health(ScopeTree.Root),
            record => record.Scope.StartsWith(
                $"{ScopeTree.Root}{Second}/", StringComparison.Ordinal));
        Assert.Equal(Second, held.For(Second, includingHidden: false).Run().Cluster);
        Assert.Equal("harsh", held.For(Second, includingHidden: false).Run().Stress);
    }

    [Fact]
    public void NothingIsAddedTogetherAcrossClusters()
    {
        // A rollup or a sum over several clusters would be a figure at a scope
        // that is in no tree (ADR-0052, amendment 2026-09-19). The set has no
        // such call; each cluster's figures are its own.
        using ClusterSurfaces held = ClusterSurfaces.Over(
            [new SnapshotOperator(Fixture("cluster.toml")),
             new SnapshotOperator(Fixture("cluster-c2.toml"))]);

        Assert.Equal(6UL, held.For(First, includingHidden: false).Figures(ScopeTree.Root).Streams);
        Assert.Equal(2UL, held.For(Second, includingHidden: false).Figures(ScopeTree.Root).Streams);
    }

    [Fact]
    public void OneSnapshotIsASetOfOneAndShowsNoChooser()
    {
        using ClusterSurfaces held =
            ClusterSurfaces.Over(new SnapshotOperator(Fixture("cluster.toml")));

        Assert.Equal([First], held.Clusters);
        Assert.False(held.Several(includingHidden: true));
        Assert.Same(held.First, held.For(null, includingHidden: false));
        Assert.Same(held.First, held.For(Second, includingHidden: false));
        Assert.False(held.Holds(Second));
        Assert.True(held.Holds(First));
        Assert.Equal(held.First.Source, held.Source);
    }

    [Fact]
    public void ARollThatEndsKeepsItsNameAndSaysItHasStoppedPublishing()
    {
        // A roll ends while an operator is reading it. The name stays in the
        // chooser — a cluster that vanished under the hand would be worse —
        // and the surface behind it answers nothing, said so.
        string area = Path.Combine(Path.GetTempPath(), $"xmip-clusters-{Guid.NewGuid():n}");
        Directory.CreateDirectory(area);
        string ending = Path.Combine(area, $"{Second}-snapshot.toml");
        File.Copy(Fixture("cluster-c2.toml"), ending);

        try
        {
            using ClusterSurfaces held = ClusterSurfaces.Over(
                [new SnapshotOperator(Fixture("cluster.toml")), new SnapshotOperator(ending)]);

            Assert.Equal([First, Second], held.Clusters);
            Assert.True(held.Publishing(Second));

            File.Delete(ending);

            Assert.Equal([First, Second], held.Clusters);
            Assert.False(held.Publishing(Second));
            Assert.True(held.Publishing(First));
            Assert.Empty(held.For(Second, includingHidden: false).Health(ScopeTree.Root));
            Assert.Contains(
                "no file at", held.For(Second, includingHidden: false).Source,
                StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(area, recursive: true);
        }
    }

    [Fact]
    public void TwoSurfacesNamingOneClusterAreRefused()
    {
        // Start-XmipTest refuses a cluster already rolling (ADR-0052,
        // amendment 2026-09-19); a face that held two of one name could not
        // say which it was showing, so it refuses for the same reason.
        InvalidOperationException refused = Assert.Throws<InvalidOperationException>(
            () => ClusterSurfaces.Over(
                [new SnapshotOperator(Fixture("cluster.toml")),
                 new SnapshotOperator(Fixture("cluster.toml"))]));

        Assert.StartsWith("REFUSED.", refused.Message, StringComparison.Ordinal);
        Assert.Contains($"cluster {First}", refused.Message, StringComparison.Ordinal);
        Assert.Contains("A cluster rolls once", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TwoSurfacesThatNameNoClusterAreRefusedToo()
    {
        InvalidOperationException refused = Assert.Throws<InvalidOperationException>(
            () => ClusterSurfaces.Over(
                [new SnapshotOperator(Fixture("snapshot.toml")),
                 new SnapshotOperator(Fixture("snapshot.toml"))]));

        Assert.Contains("name no cluster", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NoSurfaceAtAllIsRefused()
    {
        ArgumentException refused = Assert.Throws<ArgumentException>(
            () => ClusterSurfaces.Over([]));

        Assert.StartsWith("REFUSED.", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AClusterIsNamedByItsPublisherAndNeverByItsFileName()
    {
        // [run].cluster where the publisher says one; else the one first
        // segment every published scope shares. The node fixture has two —
        // its receiving and its sending node — so it names no cluster at all.
        Assert.Equal(First, ClusterSurfaces.NameOf(new SnapshotOperator(Fixture("cluster.toml"))));
        Assert.Equal(
            string.Empty, ClusterSurfaces.NameOf(new SnapshotOperator(Fixture("snapshot.toml"))));

        DateTimeOffset seen = DateTimeOffset.UtcNow;
        string receiving = $"{Cluster.Scope}/node/{Cluster.WithRole("receiving")}";
        string sending = $"{Cluster.Scope}/node/{Cluster.WithRole("sending")}";
        Assert.Equal(First, ClusterSurfaces.NameOf(new OneTree(
            new HealthRecord($"{receiving}/receive/a", HealthState.Fine, 0, "", seen),
            new HealthRecord($"{sending}/send/b", HealthState.Fine, 0, "", seen))));
    }

    [Fact]
    public void ADocumentNamesOneSnapshotOrSeveral()
    {
        using Papers one = new("""
            [Xmip]
            Surface = "snapshot"
            Snapshot = "Fixture/cluster.toml"
            """);

        Assert.Equal(["Fixture/cluster.toml"], SurfaceChoice.Snapshots(one.Configuration));

        using Papers two = new("""
            [Xmip]
            Surface = "snapshot"
            Snapshot = ["Fixture/cluster.toml", "Fixture/cluster-c2.toml"]
            """);

        Assert.Equal(
            ["Fixture/cluster.toml", "Fixture/cluster-c2.toml"],
            SurfaceChoice.Snapshots(two.Configuration));

        using ClusterSurfaces held =
            SurfaceChoice.OpenAll(two.Configuration, AppContext.BaseDirectory);

        Assert.Equal([First, Second], held.Clusters);
    }

    [Fact]
    public void AnythingButAListOfSnapshotsIsASetOfOne()
    {
        using Papers native = new("""
            [Xmip]
            Surface = "native"
            RuntimeLibrary = "lib/xmip_core_runtime.dll"
            """);

        using ClusterSurfaces held = SurfaceChoice.OpenAll(native.Configuration, native.Directory);

        Assert.False(held.Several(includingHidden: true));
        Assert.IsType<NativeOperator>(held.First);
    }

    [Fact]
    public void AClusterWhoseRunDeclaredItselfHiddenIsListedOnlyWhenAsked()
    {
        // The owner, 2026-09-29: a test cluster must be hidable in the
        // operation tools. Hiding is by what the run declared, never by the
        // cluster's name (ADR-0028 and ADR-0052, amendments 2026-09-30).
        using Hidden test = new(hidden: true);
        using ClusterSurfaces held = ClusterSurfaces.Over(
            [new SnapshotOperator(Fixture("cluster.toml")), new SnapshotOperator(test.Path)]);

        Assert.Equal([First, Second], held.Clusters);
        Assert.Equal([First], held.Listed(includingHidden: false));
        Assert.Equal([First, Second], held.Listed(includingHidden: true));
        Assert.False(held.Several(includingHidden: false));
        Assert.True(held.Several(includingHidden: true));
        Assert.True(held.Hidden(Second));
        Assert.False(held.Hidden(First));
        Assert.True(held.AnyHidden);

        // Asked for the hidden one without including it, a face is on the
        // first shown; including it, on the hidden one itself.
        Assert.Equal(First, held.Showing(Second, includingHidden: false));
        Assert.Equal(First, held.For(Second, includingHidden: false).Run().Cluster);
        Assert.Equal(Second, held.Showing(Second, includingHidden: true));
        Assert.True(held.For(Second, includingHidden: true).Run().Hidden);
        Assert.Contains("hidden test run", held.For(Second, includingHidden: true).Run().Line(),
            StringComparison.Ordinal);
    }

    [Fact]
    public void AClusterWhoseRunDeclaredNothingIsShownLikeAnyOther()
    {
        using Hidden named = new(hidden: false);
        using ClusterSurfaces held = ClusterSurfaces.Over(
            [new SnapshotOperator(Fixture("cluster.toml")), new SnapshotOperator(named.Path)]);

        Assert.Equal([First, Second], held.Listed(includingHidden: false));
        Assert.False(held.AnyHidden);
        Assert.Equal(Second, held.Showing(Second, includingHidden: false));
    }

    [Fact]
    public void AFaceHoldingOnlyHiddenClustersShowsNothingAndSaysWhy()
    {
        using Hidden test = new(hidden: true);
        using ClusterSurfaces held = ClusterSurfaces.Over(new SnapshotOperator(test.Path));

        Assert.Empty(held.Listed(includingHidden: false));
        Assert.Equal(string.Empty, held.Showing(null, includingHidden: false));
        IOperatorSurface withheld = held.For(null, includingHidden: false);
        Assert.Empty(withheld.Health(ScopeTree.Root));
        Assert.Contains("show test clusters", withheld.Source, StringComparison.Ordinal);
        Assert.Equal(Second, held.Showing(null, includingHidden: true));
    }

    /// <summary>The second cluster's fixture, its run hidden or not, in a
    /// file of its own.</summary>
    private sealed class Hidden : IDisposable
    {
        public Hidden(bool hidden)
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(), $"xmip-hidden-{Guid.NewGuid():n}.toml");
            string text = File.ReadAllText(Fixture("cluster-c2.toml"));

            if (hidden)
            {
                text = text.Replace(
                    "\n[run]\n", "\n[run]\nhidden = true\n", StringComparison.Ordinal);
            }

            File.WriteAllText(Path, text);
        }

        public string Path { get; }

        public void Dispose()
        {
            File.Delete(Path);
        }
    }

    /// <summary>A surface over records and nothing else, for naming alone.</summary>
    private sealed class OneTree(params HealthRecord[] records) : IOperatorSurface
    {
        public string Source => "test";

        public IReadOnlyList<HealthRecord> Health(string scope)
        {
            return records;
        }

        public MeasurementRecord? Measure(string scope, Counted counted)
        {
            return null;
        }

        public string PauseScope(string scope, string who)
        {
            return "no";
        }

        public string ResumeScope(string scope)
        {
            return "no";
        }
    }

    /// <summary>A host's TOML document in a directory of its own.</summary>
    private sealed class Papers : IDisposable
    {
        public Papers(string toml)
        {
            Directory = Path.Combine(Path.GetTempPath(), $"xmip-cluster-set-{Guid.NewGuid():n}");
            System.IO.Directory.CreateDirectory(Directory);
            string file = Path.Combine(Directory, "xmip.gui.toml");
            File.WriteAllText(file, toml);
            Configuration = TomlDocument.Read(file);
        }

        public string Directory { get; }

        public IConfigurationRoot Configuration { get; }

        public void Dispose()
        {
            (Configuration as IDisposable)?.Dispose();
            System.IO.Directory.Delete(Directory, recursive: true);
        }
    }
}
