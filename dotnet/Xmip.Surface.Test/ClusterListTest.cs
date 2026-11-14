using Microsoft.Extensions.Configuration;

namespace Xmip.Surface.Test;

/// <summary>
/// A host names one surface per cluster, each of its own kind (the design
/// approved 2026-11-12, section 3.4): one cluster local at its monitoring
/// node, another a snapshot. Each is opened by the one rule a single surface
/// is, refused in its own keys, and a surface that has published nothing yet
/// — a node not answering when the host starts — is named by its first
/// publication rather than refused or left nameless.
/// </summary>
public sealed class ClusterListTest
{
    private static readonly TestCluster Cluster = TestCluster.Read();

    private static readonly string Second =
        new SnapshotOperator(Fixture("cluster-cl2.xmip")).Run().Cluster;

    [Fact]
    public void ADocumentNamesOneSurfacePerClusterEachOfItsOwnKind()
    {
        string endpoint = $"xmip-nobody-{Guid.NewGuid():n}";
        using Papers papers = new($"""
            [Xmip]
            RuntimeLibrary = "lib/xmip_core_runtime.dll"

            [[Xmip.Clusters]]
            Surface = "local"
            Local = "{endpoint}"

            [[Xmip.Clusters]]
            Surface = "snapshot"
            Snapshot = "{Escaped(Fixture("cluster.xmip"))}"
            """);

        Assert.True(SurfaceChoice.IsChosen(papers.Configuration));
        Assert.Equal(
            [endpoint, Fixture("cluster.xmip")], SurfaceChoice.Places(papers.Configuration));

        using ClusterSurfaces held = SurfaceChoice.OpenAll(papers.Configuration, papers.Directory);

        // The local node answers nothing, so it names nothing yet and is
        // refused with nothing; the snapshot names its cluster.
        Assert.Equal(2, held.Count);
        Assert.Equal([string.Empty, Cluster.Name], held.Clusters);
        Assert.IsType<LocalOperator>(held.For(string.Empty, includingHidden: false));
        Assert.IsType<SnapshotOperator>(held.For(Cluster.Name, includingHidden: false));
    }

    [Fact]
    public void TheListWinsOverTheSurfaceTheShippedDocumentNames()
    {
        using Papers papers = new($$"""
            [Xmip]
            Surface = "local"
            Clusters = [
              { Surface = "snapshot", Snapshot = "{{Escaped(Fixture("cluster.xmip"))}}" },
              { Surface = "snapshot", Snapshot = "{{Escaped(Fixture("cluster-cl2.xmip"))}}" },
            ]
            """);

        using ClusterSurfaces held = SurfaceChoice.OpenAll(papers.Configuration, papers.Directory);

        Assert.Equal([Cluster.Name, Second], held.Clusters);

        // A command and the prompt read one publication: the first named.
        IOperatorSurface first = SurfaceChoice.Stated(
            SurfaceLine.None, papers.Configuration, papers.Directory, papers.Directory);
        Assert.Equal(Fixture("cluster.xmip"), Assert.IsType<SnapshotOperator>(first).Path);
    }

    [Fact]
    public void AClusterTheListCannotOpenIsRefusedInItsOwnKeys()
    {
        using Papers papers = new($"""
            [Xmip]
            [[Xmip.Clusters]]
            Surface = "snapshot"
            Snapshot = "{Escaped(Fixture("cluster.xmip"))}"

            [[Xmip.Clusters]]
            Surface = "snapshot"
            """);

        InvalidOperationException refused = Assert.Throws<InvalidOperationException>(
            () => SurfaceChoice.OpenAll(papers.Configuration, papers.Directory));

        Assert.Equal(
            "Xmip:Clusters:1:Surface is \"snapshot\" and Xmip:Clusters:1:Snapshot names no file",
            refused.Message);
    }

    [Fact]
    public void ASurfaceThatHasPublishedNothingYetIsNamedByItsFirstPublication()
    {
        string area = Path.Combine(Path.GetTempPath(), $"xmip-cluster-list-{Guid.NewGuid():n}");
        Directory.CreateDirectory(area);
        string later = Path.Combine(area, "later.xmip");

        try
        {
            using ClusterSurfaces held = ClusterSurfaces.Over(
                [new SnapshotOperator(later), new SnapshotOperator(Fixture("cluster.xmip"))]);

            Assert.Equal([string.Empty, Cluster.Name], held.Clusters);
            Assert.False(held.Publishing(string.Empty));

            File.Copy(Fixture("cluster-cl2.xmip"), later);

            Assert.Equal([Second, Cluster.Name], held.Clusters);
            Assert.True(held.Publishing(Second));
            Assert.Equal(Second, held.For(Second, includingHidden: false).Run().Cluster);
        }
        finally
        {
            Directory.Delete(area, recursive: true);
        }
    }

    [Fact]
    public void TwoSurfacesThatHavePublishedNothingYetAreNotRefused()
    {
        string area = Path.Combine(Path.GetTempPath(), $"xmip-cluster-list-{Guid.NewGuid():n}");

        using ClusterSurfaces held = ClusterSurfaces.Over(
            [new SnapshotOperator(Path.Combine(area, "a.xmip")),
             new SnapshotOperator(Path.Combine(area, "b.xmip"))]);

        Assert.Equal([string.Empty, string.Empty], held.Clusters);
    }

    [Fact]
    public void ALaterNameAnotherClusterHoldsIsNotTakenTwice()
    {
        string area = Path.Combine(Path.GetTempPath(), $"xmip-cluster-list-{Guid.NewGuid():n}");
        Directory.CreateDirectory(area);
        string later = Path.Combine(area, "later.xmip");

        try
        {
            using ClusterSurfaces held = ClusterSurfaces.Over(
                [new SnapshotOperator(Fixture("cluster.xmip")), new SnapshotOperator(later)]);

            File.Copy(Fixture("cluster.xmip"), later);

            Assert.Equal([Cluster.Name, string.Empty], held.Clusters);
        }
        finally
        {
            Directory.Delete(area, recursive: true);
        }
    }

    private static string Fixture(string name)
    {
        return Path.Combine(AppContext.BaseDirectory, "Fixture", name);
    }

    // A path written into a TOML basic string.
    private static string Escaped(string path)
    {
        return path.Replace("\\", "\\\\", StringComparison.Ordinal);
    }

    /// <summary>A host's TOML document in a directory of its own.</summary>
    private sealed class Papers : IDisposable
    {
        public Papers(string toml)
        {
            Directory = Path.Combine(Path.GetTempPath(), $"xmip-cluster-list-{Guid.NewGuid():n}");
            System.IO.Directory.CreateDirectory(Directory);
            string file = Path.Combine(Directory, "gui.toml");
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
