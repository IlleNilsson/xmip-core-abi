using Microsoft.Extensions.Configuration;

namespace Xmip.Surface;

/// <summary>
/// The surface a host reads is chosen in its configuration, never guessed
/// (ADR-0052 clause 3). In the host's <c>[Xmip]</c> table: <c>Surface</c> is
/// <c>"native"</c> — a runtime library found by <see cref="RuntimeLibrary"/>'s
/// rule — or <c>"snapshot"</c> — a file at <c>Snapshot</c>. Nothing is chosen
/// by finding a file in a temp directory, and a configuration that names
/// neither is refused rather than defaulted.
/// </summary>
public static class SurfaceChoice
{
    /// <summary>The key naming the surface.</summary>
    public const string SurfaceKey = "Xmip:Surface";

    /// <summary>The key naming the snapshot file, when the surface is one.</summary>
    public const string SnapshotKey = "Xmip:Snapshot";

    /// <summary>The word for the surface over a runtime library.</summary>
    public const string Native = "native";

    /// <summary>The word for the surface over a published snapshot.</summary>
    public const string Snapshot = "snapshot";

    /// <summary>Open the surface the configuration names. Relative paths
    /// resolve against <paramref name="basePath"/>, the directory the
    /// configuration's paths are written from.</summary>
    /// <exception cref="InvalidOperationException">The configuration names no
    /// surface, a surface this build does not know, or a snapshot with no
    /// path.</exception>
    public static IOperatorSurface Open(IConfiguration configuration, string basePath)
    {
        string chosen = configuration[SurfaceKey]?.Trim() ?? string.Empty;

        if (string.Equals(chosen, Native, StringComparison.OrdinalIgnoreCase))
        {
            return new NativeOperator(RuntimeLibrary.Find(configuration, basePath));
        }

        if (string.Equals(chosen, Snapshot, StringComparison.OrdinalIgnoreCase))
        {
            string? path = configuration[SnapshotKey];

            return string.IsNullOrWhiteSpace(path)
                ? throw new InvalidOperationException(
                    $"{SurfaceKey} is \"{Snapshot}\" and {SnapshotKey} names no file")
                : new SnapshotOperator(TomlDocument.Resolve(path, basePath));
        }

        throw new InvalidOperationException(
            chosen.Length == 0
                ? $"{SurfaceKey} is not set; it is \"{Native}\" or \"{Snapshot}\""
                : $"{SurfaceKey} is \"{chosen}\"; it is \"{Native}\" or \"{Snapshot}\"");
    }
}
