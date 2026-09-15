using Microsoft.Extensions.Configuration;

namespace Xmip.Surface;

/// <summary>
/// The surface a host reads is chosen in its configuration, never guessed
/// (ADR-0052 clause 3). In the host's <c>[Xmip]</c> table: <c>Surface</c> is
/// <c>"native"</c> — a runtime library found by <see cref="RuntimeLibrary"/>'s
/// rule — <c>"snapshot"</c> — a file at <c>Snapshot</c> — or <c>"remote"</c> —
/// a web host at <c>Url</c>, followed over its surface hub (ADR-0052,
/// amendment 2026-09-15). Nothing is chosen by finding a file in a temp
/// directory, and a configuration that names none is refused rather than
/// defaulted.
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

    /// <summary>The key naming the web host, when the surface is remote.</summary>
    public const string UrlKey = "Xmip:Url";

    /// <summary>The word for the surface over a web host on another machine.</summary>
    public const string Remote = "remote";

    /// <summary>Whether the configuration names a surface at all. A host
    /// that has one runtime-discovery fallback — the PowerShell module, whose
    /// prompt follows whatever library the one rule finds — asks this before
    /// <see cref="Open"/>, so that nothing configured is said as such rather
    /// than refused.</summary>
    public static bool IsChosen(IConfiguration configuration)
    {
        return !string.IsNullOrWhiteSpace(configuration[SurfaceKey]);
    }

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

        if (string.Equals(chosen, Remote, StringComparison.OrdinalIgnoreCase))
        {
            string? url = configuration[UrlKey];

            return RemoteOperator.IsWebHost(url)
                ? new RemoteOperator(new Uri(url, UriKind.Absolute))
                : throw new InvalidOperationException(
                    $"{SurfaceKey} is \"{Remote}\" and {UrlKey} names no web host");
        }

        string words = $"\"{Native}\", \"{Snapshot}\" or \"{Remote}\"";

        throw new InvalidOperationException(
            chosen.Length == 0
                ? $"{SurfaceKey} is not set; it is {words}"
                : $"{SurfaceKey} is \"{chosen}\"; it is {words}");
    }
}
