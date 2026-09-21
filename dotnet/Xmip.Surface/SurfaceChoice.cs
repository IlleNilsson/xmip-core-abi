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

    /// <summary>The key naming the snapshot file, when the surface is one. It
    /// takes one path, or a list of them — one per cluster (ADR-0052,
    /// amendment 2026-09-20).</summary>
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

    /// <summary>
    /// Every snapshot path the document names, in the order it names them:
    /// one where <c>Snapshot</c> is a path, several where it is a list —
    /// <c>Snapshot = ["…/orders-snapshot.toml", "…/partner-snapshot.toml"]</c> in a
    /// document, <c>--Xmip:Snapshot:0=… --Xmip:Snapshot:1=…</c> on a line.
    /// Empty where it names none. A list wins over a path: the two sit at the
    /// same key from different sources, and a line naming two clusters must
    /// not be quietly replaced by the one the shipped document names.
    /// </summary>
    public static IReadOnlyList<string> Snapshots(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        string[] listed =
        [
            .. configuration.GetSection(SnapshotKey).GetChildren()
                .Select(child => child.Value)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(path => path!),
        ];

        if (listed.Length > 0)
        {
            return listed;
        }

        string? one = configuration[SnapshotKey];

        return string.IsNullOrWhiteSpace(one) ? [] : [one];
    }

    /// <summary>
    /// Every cluster the configuration names, as one set a face navigates
    /// (ADR-0052, amendment 2026-09-20). A snapshot surface over a list of
    /// paths is one surface per path; every other choice is the one surface
    /// <see cref="Open"/> returns, held as a set of one, so a face reads the
    /// same shape whatever it was given.
    /// </summary>
    /// <exception cref="InvalidOperationException">What <see cref="Open"/>
    /// refuses, or two paths publishing one cluster.</exception>
    public static ClusterSurfaces OpenAll(IConfiguration configuration, string basePath)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        IReadOnlyList<string> paths = Listed(configuration);

        return paths.Count > 0
            ? ClusterSurfaces.Over(
                paths.Select(path => new SnapshotOperator(TomlDocument.Resolve(path, basePath))))
            : ClusterSurfaces.Over(Open(configuration, basePath));
    }

    /// <summary>
    /// The one surface a host reads where it reads one: the document's choice,
    /// and the first cluster where the document names several. A command and a
    /// prompt answer one publication, because a rollup or a sum over two
    /// clusters would be a figure at a scope that is in neither tree (ADR-0052,
    /// amendment 2026-09-20).
    /// </summary>
    /// <exception cref="InvalidOperationException">What <see cref="Open"/>
    /// refuses.</exception>
    public static IOperatorSurface OpenFirst(IConfiguration configuration, string basePath)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        IReadOnlyList<string> paths = Listed(configuration);

        return paths.Count > 0
            ? new SnapshotOperator(TomlDocument.Resolve(paths[0], basePath))
            : Open(configuration, basePath);
    }

    // The snapshot paths, where a snapshot is what the document chose; none
    // otherwise, so that every other surface goes through Open as it did.
    private static IReadOnlyList<string> Listed(IConfiguration configuration)
    {
        string chosen = configuration[SurfaceKey]?.Trim() ?? string.Empty;

        return string.Equals(chosen, Snapshot, StringComparison.OrdinalIgnoreCase)
            ? Snapshots(configuration)
            : [];
    }
}
