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
    private static string Fixture(string name)
    {
        return Path.Combine(AppContext.BaseDirectory, "Fixture", name);
    }

    [Fact]
    public void TwoSnapshotsAreTwoClustersEachWithItsOwnTree()
    {
        using ClusterSurfaces held = ClusterSurfaces.Over(
            [new SnapshotOperator(Fixture("cluster.toml")),
             new SnapshotOperator(Fixture("cluster-c2.toml"))]);

        Assert.Equal(["C1", "C2"], held.Clusters);
        Assert.True(held.Several);
        Assert.Equal(2, held.Count);
        Assert.Equal("2 clusters — C1, C2", held.Source);

        // Each answers its own root and nothing else: a cluster is a whole
        // scope tree, and neither can see into the other.
        Assert.Contains(
            held.For("C1").Health(ScopeTree.Root),
            record => record.Scope.StartsWith("xmip:///C1/", StringComparison.Ordinal));
        Assert.DoesNotContain(
            held.For("C1").Health(ScopeTree.Root),
            record => record.Scope.StartsWith("xmip:///C2/", StringComparison.Ordinal));
        Assert.Equal("C2", held.For("C2").Run().Cluster);
        Assert.Equal("harsh", held.For("C2").Run().Stress);
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

        Assert.Equal(6UL, held.For("C1").Figures(ScopeTree.Root).Streams);
        Assert.Equal(2UL, held.For("C2").Figures(ScopeTree.Root).Streams);
    }

    [Fact]
    public void OneSnapshotIsASetOfOneAndShowsNoChooser()
    {
        using ClusterSurfaces held =
            ClusterSurfaces.Over(new SnapshotOperator(Fixture("cluster.toml")));

        Assert.Equal(["C1"], held.Clusters);
        Assert.False(held.Several);
        Assert.Same(held.First, held.For(null));
        Assert.Same(held.First, held.For("C9"));
        Assert.False(held.Holds("C9"));
        Assert.True(held.Holds("C1"));
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
        string ending = Path.Combine(area, "C2-snapshot.toml");
        File.Copy(Fixture("cluster-c2.toml"), ending);

        try
        {
            using ClusterSurfaces held = ClusterSurfaces.Over(
                [new SnapshotOperator(Fixture("cluster.toml")), new SnapshotOperator(ending)]);

            Assert.Equal(["C1", "C2"], held.Clusters);
            Assert.True(held.Publishing("C2"));

            File.Delete(ending);

            Assert.Equal(["C1", "C2"], held.Clusters);
            Assert.False(held.Publishing("C2"));
            Assert.True(held.Publishing("C1"));
            Assert.Empty(held.For("C2").Health(ScopeTree.Root));
            Assert.Contains("no file at", held.For("C2").Source, StringComparison.Ordinal);
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
        Assert.Contains("cluster C1", refused.Message, StringComparison.Ordinal);
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
        // segment every published scope shares. The lab fixture has two —
        // edge-01 and edge-02 — so it names no cluster at all.
        Assert.Equal("C1", ClusterSurfaces.NameOf(new SnapshotOperator(Fixture("cluster.toml"))));
        Assert.Equal(
            string.Empty, ClusterSurfaces.NameOf(new SnapshotOperator(Fixture("snapshot.toml"))));

        DateTimeOffset seen = DateTimeOffset.UtcNow;
        Assert.Equal("Z8", ClusterSurfaces.NameOf(new OneTree(
            new HealthRecord("xmip:///Z8/node/alpha/receive/a", HealthState.Fine, 0, "", seen),
            new HealthRecord("xmip:///Z8/node/gamma/send/b", HealthState.Fine, 0, "", seen))));
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

        Assert.Equal(["C1", "C2"], held.Clusters);
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

        Assert.False(held.Several);
        Assert.IsType<NativeOperator>(held.First);
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
