using Microsoft.Extensions.Configuration;

namespace Xmip.Surface.Test;

/// <summary>
/// The surface is chosen in the host's configuration and never guessed
/// (ADR-0052 clause 3): a TOML document names native or snapshot, and a
/// document that names neither is refused.
/// </summary>
public sealed class SurfaceChoiceTest
{
    [Fact]
    public void ASnapshotSurfaceReadsTheFileTheDocumentNames()
    {
        using Document document = new("""
            [Xmip]
            Surface = "snapshot"
            Snapshot = "Fixture/snapshot.toml"
            """);

        string beside = AppContext.BaseDirectory;
        IOperatorSurface surface = SurfaceChoice.Open(document.Configuration, beside);

        SnapshotOperator snapshot = Assert.IsType<SnapshotOperator>(surface);
        Assert.Equal(Path.Combine(beside, "Fixture", "snapshot.toml"), snapshot.Path);
        Assert.Equal(5, snapshot.Health(ScopeTree.Root).Count);
    }

    [Fact]
    public void ANativeSurfaceIsFoundByTheDiscoveryRule()
    {
        using Document document = new("""
            [Xmip]
            Surface = "Native"
            RuntimeLibrary = "lib/xmip_core_runtime.dll"
            """);

        IOperatorSurface surface = SurfaceChoice.Open(document.Configuration, document.Directory);

        NativeOperator native = Assert.IsType<NativeOperator>(surface);
        Assert.Equal(Path.Combine(document.Directory, "lib", "xmip_core_runtime.dll"), native.Path);
        Assert.False(native.IsLoaded);
        Assert.Contains("no runtime library at", native.Reason, StringComparison.Ordinal);
        Assert.StartsWith("NATIVE — ", native.Source, StringComparison.Ordinal);
        Assert.Empty(native.Health(ScopeTree.Root));
        native.Dispose();
    }

    [Fact]
    public void NoSurfaceIsRefusedNotDefaulted()
    {
        using Document document = new("""
            [Xmip]
            RuntimeLibrary = "lib/xmip_core_runtime.dll"
            """);

        Assert.False(SurfaceChoice.IsChosen(document.Configuration));

        InvalidOperationException refused = Assert.Throws<InvalidOperationException>(
            () => SurfaceChoice.Open(document.Configuration, document.Directory));

        Assert.Contains("Xmip:Surface is not set", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ASurfaceIsChosenTheMomentTheKeyIsSet()
    {
        using Document document = new("""
            [Xmip]
            Surface = "snapshot"
            """);

        Assert.True(SurfaceChoice.IsChosen(document.Configuration));
    }

    [Fact]
    public void AnUnknownSurfaceIsRefused()
    {
        using Document document = new("""
            [Xmip]
            Surface = "sample"
            """);

        InvalidOperationException refused = Assert.Throws<InvalidOperationException>(
            () => SurfaceChoice.Open(document.Configuration, document.Directory));

        Assert.Contains("\"sample\"", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ARemoteSurfaceFollowsTheWebHostTheDocumentNames()
    {
        using Document document = new("""
            [Xmip]
            Surface = "remote"
            Url = "http://another-host:5087"
            """);

        IOperatorSurface surface = SurfaceChoice.Open(document.Configuration, document.Directory);

        using RemoteOperator remote = Assert.IsType<RemoteOperator>(surface);
        Assert.Equal(new Uri("http://another-host:5087/surface"), remote.Hub);
    }

    [Fact]
    public void ARemoteSurfaceWithoutAWebHostIsRefused()
    {
        using Document document = new("""
            [Xmip]
            Surface = "remote"
            Url = "another-host"
            """);

        InvalidOperationException refused = Assert.Throws<InvalidOperationException>(
            () => SurfaceChoice.Open(document.Configuration, document.Directory));

        Assert.Contains("Xmip:Url names no web host", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ASnapshotWithoutAPathIsRefused()
    {
        using Document document = new("""
            [Xmip]
            Surface = "snapshot"
            """);

        InvalidOperationException refused = Assert.Throws<InvalidOperationException>(
            () => SurfaceChoice.Open(document.Configuration, document.Directory));

        Assert.Contains("Xmip:Snapshot names no file", refused.Message, StringComparison.Ordinal);
    }

    // The line over the document: one precedence for xmip-cli's --remote,
    // --snapshot and --runtime, a cmdlet's -Remote, -Snapshot and -Library,
    // and the snapshot the prompt is told to follow (ADR-0052, amendments
    // 2026-09-18 and 2026-09-20). Held here since 2026-09-24; the executable's
    // own tests held it before, and the prompt wrote it a second time.
    private static readonly string Beside = Path.GetFullPath(
        Path.Combine(Path.GetTempPath(), "xmip-surface-choice", "bin"));

    [Fact]
    public void WithNothingStatedTheDocumentChooses()
    {
        IOperatorSurface chosen = SurfaceChoice.Stated(
            SurfaceLine.None, Pairs(("Surface", "snapshot"), ("Snapshot", "c1.toml")),
            Beside, Beside);

        Assert.Equal(Path.Combine(Beside, "c1.toml"), Assert.IsType<SnapshotOperator>(chosen).Path);
    }

    [Fact]
    public void ARemoteHostStatedWinsOverEverything()
    {
        IOperatorSurface chosen = SurfaceChoice.Stated(
            new SurfaceLine("http://elsewhere:5087", "C2.toml", "mine.dll"),
            Pairs(("Surface", "snapshot"), ("Snapshot", "c1.toml")),
            Beside,
            Beside);

        using RemoteOperator remote = Assert.IsType<RemoteOperator>(chosen);
        Assert.Equal(new Uri("http://elsewhere:5087"), remote.Host);
    }

    [Fact]
    public void ASnapshotStatedNamesWhichClusterAndWinsOverARuntime()
    {
        IOperatorSurface chosen = SurfaceChoice.Stated(
            new SurfaceLine(Snapshot: "C2-snapshot.toml", Runtime: "mine.dll"),
            Pairs(("Surface", "snapshot"), ("Snapshot", "c1.toml")),
            Beside,
            Beside);

        Assert.Equal(
            Path.Combine(Beside, "C2-snapshot.toml"), Assert.IsType<SnapshotOperator>(chosen).Path);
    }

    [Fact]
    public void ARuntimeStatedWinsOverTheDocumentsSurface()
    {
        IOperatorSurface chosen = SurfaceChoice.Stated(
            new SurfaceLine(Runtime: "mine.dll"),
            Pairs(("Surface", "snapshot"), ("Snapshot", "c1.toml")),
            Beside,
            Beside);

        using NativeOperator native = Assert.IsType<NativeOperator>(chosen);
        Assert.Equal(Path.GetFullPath("mine.dll"), native.Path);
    }

    [Fact]
    public void NoSurfaceNamedAnywhereMeansTheRuntimeRule()
    {
        IOperatorSurface chosen = SurfaceChoice.Stated(
            SurfaceLine.None, Pairs(("RuntimeLibrary", "configured.dll")), Beside, Beside);

        using NativeOperator native = Assert.IsType<NativeOperator>(chosen);
        Assert.Equal(Path.Combine(Beside, "configured.dll"), native.Path);
    }

    [Fact]
    public void ADocumentNamingSeveralClustersIsReadAsItsFirst()
    {
        // A command and a prompt answer at one scope, and a tree of two
        // clusters is a scope in neither. The web monitor is where an operator
        // moves between them.
        IOperatorSurface chosen = SurfaceChoice.Stated(
            SurfaceLine.None,
            Pairs(("Surface", "snapshot"), ("Snapshot:0", "c1.toml"), ("Snapshot:1", "c2.toml")),
            Beside,
            Beside);

        Assert.Equal(Path.Combine(Beside, "c1.toml"), Assert.IsType<SnapshotOperator>(chosen).Path);
    }

    [Fact]
    public void ASurfaceThisBuildDoesNotKnowIsStillRefusedWithNothingStated()
    {
        Assert.Throws<InvalidOperationException>(() => SurfaceChoice.Stated(
            SurfaceLine.None, Pairs(("Surface", "carrier-pigeon")), Beside, Beside));
    }

    [Fact]
    public void AnsweringReturnsASurfaceThatAnswersAndSaysWhyOfOneThatDoesNot()
    {
        using Document document = new("""
            [Xmip]
            Surface = "snapshot"
            Snapshot = "no-such-snapshot.toml"
            """);
        string file = Path.Combine(document.Directory, "xmip.gui.toml");

        Assert.Null(SurfaceChoice.Answering(SurfaceLine.None, file, Beside, out string reason));
        Assert.Contains("no-such-snapshot.toml", reason, StringComparison.Ordinal);

        string fixture = Path.Combine(AppContext.BaseDirectory, "Fixture", "snapshot.toml");
        IOperatorSurface? answered = SurfaceChoice.Answering(
            new SurfaceLine(Snapshot: fixture), file, Beside, out string none);

        Assert.Equal(string.Empty, none);
        Assert.Equal(5, Assert.IsType<SnapshotOperator>(answered).Health(ScopeTree.Root).Count);
    }

    [Fact]
    public void AnsweringNamesTheDocumentThatRefused()
    {
        using Document document = new("""
            [Xmip]
            Surface = "sample"
            """);
        string file = Path.Combine(document.Directory, "xmip.gui.toml");

        Assert.Null(SurfaceChoice.Answering(SurfaceLine.None, file, Beside, out string reason));
        Assert.StartsWith("xmip.gui.toml: ", reason, StringComparison.Ordinal);
    }

    private static IConfiguration Pairs(params (string Key, string Value)[] pairs)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(pairs.Select(pair =>
                new KeyValuePair<string, string?>($"Xmip:{pair.Key}", pair.Value)))
            .Build();
    }

    /// <summary>A host's TOML document written to a directory of its own and
    /// read the way a host reads it.</summary>
    private sealed class Document : IDisposable
    {
        public Document(string toml)
        {
            Directory = Path.Combine(Path.GetTempPath(), $"xmip-surface-choice-{Guid.NewGuid():n}");
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
