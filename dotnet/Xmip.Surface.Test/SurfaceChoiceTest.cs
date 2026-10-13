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
    public void ARemoteThatNamesNoNodeIsRefusedInOneSentence()
    {
        // --remote and -Remote say the same thing, and the precedence refuses
        // the line itself rather than blaming the document. What an address
        // that names something is, the runtime judges as it opens it.
        SurfaceLine line = new(Remote: " ");

        Assert.Null(line.Refusal);
        Assert.Equal(
            "A remote needs a node's operate address, like node1:7401.",
            RemoteOperator.Refusal(" "));
        Assert.Null(new SurfaceLine(Remote: "node1:7401").Refusal);
        Assert.Null(SurfaceLine.None.Refusal);
    }

    [Fact]
    public void WhoPausedIsTheNameStatedElseThisUser()
    {
        Assert.Equal("ilian", ScopeOperation.Who(" ilian "));
        Assert.Equal(Environment.UserName, ScopeOperation.Who(null));
        Assert.Equal(Environment.UserName, ScopeOperation.Who(" "));
    }

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
    public void ALocalSurfaceFollowsTheEndpointTheDocumentNames()
    {
        string endpoint = $"xmip-nobody-{Guid.NewGuid():n}";
        using Document document = new($"""
            [Xmip]
            Surface = "Local"
            Local = "{endpoint}"
            """);

        IOperatorSurface surface = SurfaceChoice.Open(document.Configuration, document.Directory);

        using LocalOperator local = Assert.IsType<LocalOperator>(surface);
        Assert.False(SurfaceChoice.Answers(local));
        Assert.Contains("unreachable", local.Source, StringComparison.Ordinal);
        Assert.StartsWith("LOCAL — ", local.Source, StringComparison.Ordinal);
        Assert.Empty(local.Health(ScopeTree.Root));
    }

    [Fact]
    public void ALocalSurfaceNamingNoEndpointNamesNoNodeAndSaysSo()
    {
        using Document document = new("""
            [Xmip]
            Surface = "local"
            """);

        using LocalOperator local = Assert.IsType<LocalOperator>(
            SurfaceChoice.Open(document.Configuration, document.Directory));
        Assert.False(SurfaceChoice.Answers(local));
        Assert.Contains(LocalOperator.Unnamed, local.Source, StringComparison.Ordinal);
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
    public void ARemoteSurfaceFollowsTheNodeTheDocumentNames()
    {
        using Document document = new("""
            [Xmip]
            Surface = "remote"
            Address = "another-host:7401"
            """);

        IOperatorSurface surface = SurfaceChoice.Open(document.Configuration, document.Directory);

        using RemoteOperator remote = Assert.IsType<RemoteOperator>(surface);
        Assert.Equal("another-host:7401", remote.Address);
    }

    [Fact]
    public void ARemoteSurfaceWithoutANodeIsRefused()
    {
        using Document document = new("""
            [Xmip]
            Surface = "remote"
            """);

        InvalidOperationException refused = Assert.Throws<InvalidOperationException>(
            () => SurfaceChoice.Open(document.Configuration, document.Directory));

        Assert.Contains("Xmip:Address names no node", refused.Message, StringComparison.Ordinal);
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
            SurfaceLine.None, Pairs(("Surface", "snapshot"), ("Snapshot", "a.toml")),
            Beside, Beside);

        Assert.Equal(Path.Combine(Beside, "a.toml"), Assert.IsType<SnapshotOperator>(chosen).Path);
    }

    [Fact]
    public void ARemoteNodeStatedWinsOverEverything()
    {
        IOperatorSurface chosen = SurfaceChoice.Stated(
            new SurfaceLine("elsewhere:7401", "b.toml", "mine.dll"),
            Pairs(("Surface", "snapshot"), ("Snapshot", "a.toml")),
            Beside,
            Beside);

        using RemoteOperator remote = Assert.IsType<RemoteOperator>(chosen);
        Assert.Equal("elsewhere:7401", remote.Address);
    }

    [Fact]
    public void ASnapshotStatedNamesWhichClusterAndWinsOverARuntime()
    {
        IOperatorSurface chosen = SurfaceChoice.Stated(
            new SurfaceLine(Snapshot: "b-snapshot.toml", Runtime: "mine.dll"),
            Pairs(("Surface", "snapshot"), ("Snapshot", "a.toml")),
            Beside,
            Beside);

        Assert.Equal(
            Path.Combine(Beside, "b-snapshot.toml"), Assert.IsType<SnapshotOperator>(chosen).Path);
    }

    [Fact]
    public void ARuntimeStatedChoosesNoSurface()
    {
        IOperatorSurface chosen = SurfaceChoice.Stated(
            new SurfaceLine(Runtime: "mine.dll"),
            Pairs(("Surface", "snapshot"), ("Snapshot", "a.toml")),
            Beside,
            Beside);

        Assert.Equal(Path.Combine(Beside, "a.toml"), Assert.IsType<SnapshotOperator>(chosen).Path);
    }

    [Fact]
    public void ALocalNodeStatedWinsOverTheSnapshotAndTheDocument()
    {
        string endpoint = $"xmip-nobody-{Guid.NewGuid():n}";
        IOperatorSurface chosen = SurfaceChoice.Stated(
            new SurfaceLine(Snapshot: "b-snapshot.toml", Local: endpoint),
            Pairs(("Surface", "snapshot"), ("Snapshot", "a.toml")),
            Beside,
            Beside);

        using LocalOperator local = Assert.IsType<LocalOperator>(chosen);
        Assert.EndsWith(endpoint, local.Address, StringComparison.Ordinal);
    }

    [Fact]
    public void NoSurfaceNamedAnywhereNamesNoNodeAndSaysSo()
    {
        IOperatorSurface chosen = SurfaceChoice.Stated(
            SurfaceLine.None, Pairs(("RuntimeLibrary", "configured.dll")), Beside, Beside);

        using LocalOperator local = Assert.IsType<LocalOperator>(chosen);
        Assert.False(SurfaceChoice.Answers(local));
        Assert.Contains(LocalOperator.Unnamed, local.Source, StringComparison.Ordinal);
    }

    [Fact]
    public void ADocumentNamingSeveralClustersIsReadAsItsFirst()
    {
        // A command and a prompt answer at one scope, and a tree of two
        // clusters is a scope in neither. The web monitor is where an operator
        // moves between them.
        IOperatorSurface chosen = SurfaceChoice.Stated(
            SurfaceLine.None,
            Pairs(("Surface", "snapshot"), ("Snapshot:0", "a.toml"), ("Snapshot:1", "b.toml")),
            Beside,
            Beside);

        Assert.Equal(Path.Combine(Beside, "a.toml"), Assert.IsType<SnapshotOperator>(chosen).Path);
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
