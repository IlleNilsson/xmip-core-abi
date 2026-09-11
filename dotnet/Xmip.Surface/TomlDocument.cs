using Microsoft.Extensions.Configuration;
using Tomlyn.Extensions.Configuration;

namespace Xmip.Surface;

/// <summary>
/// The one way a .NET surface reads a TOML document (ADR-0052 clause 1). On
/// disk the estate is TOML — a host's <c>xmip.gui.toml</c>, a node
/// configuration, a published snapshot — and JSON is reserved for memory and
/// the wire; so there is one reader, and no host adds a JSON source.
/// </summary>
public static class TomlDocument
{
    /// <summary>Read one document. A file that is not there reads as an empty
    /// document; whoever asked decides whether that is a problem.</summary>
    public static IConfigurationRoot Read(string path)
    {
        return new ConfigurationBuilder()
            .AddTomlFile(path, optional: true, reloadOnChange: false)
            .Build();
    }

    /// <summary>Add a document to a host's configuration, watched for change.
    /// A relative path resolves the way the host's builder resolves files —
    /// its content root.</summary>
    public static IConfigurationBuilder Add(
        IConfigurationBuilder builder, string path, bool optional)
    {
        return builder.AddTomlFile(path, optional, reloadOnChange: true);
    }

    /// <summary>A path a document names: absolute as written, relative joined
    /// to <paramref name="basePath"/>, which is the directory the document's
    /// paths are written from.</summary>
    public static string Resolve(string path, string basePath)
    {
        return Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(basePath, path));
    }
}
