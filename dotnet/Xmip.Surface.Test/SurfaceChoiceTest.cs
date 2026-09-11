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

        InvalidOperationException refused = Assert.Throws<InvalidOperationException>(
            () => SurfaceChoice.Open(document.Configuration, document.Directory));

        Assert.Contains("Xmip:Surface is not set", refused.Message, StringComparison.Ordinal);
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
