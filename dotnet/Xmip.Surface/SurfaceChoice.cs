using Microsoft.Extensions.Configuration;

namespace Xmip.Surface;

/// <summary>
/// The surface a host reads is chosen in its configuration, never guessed
/// (ADR-0052 clause 3). In the host's <c>[Xmip]</c> table: <c>Surface</c> is
/// <c>"local"</c> — a node on this machine, at the local endpoint
/// <c>Local</c> names, followed over gRPC (ADR-0014, amendment 2026-10-13) —
/// <c>"snapshot"</c> — a file at <c>Snapshot</c> — or <c>"remote"</c> — a
/// node's operate listener at <c>Address</c>, followed over gRPC (ADR-0014,
/// amendment 2026-10-12), presenting and trusting what
/// <see cref="SecureSurface"/> reads from the same table (ADR-0063 clause 1).
/// Nothing is chosen by finding a file in a temp directory, and a
/// configuration that names none is refused rather than defaulted. The
/// runtime library, found by <see cref="RuntimeLibrary"/>'s rule, is what
/// every surface calls the runtime through — the gRPC client among it — and
/// chooses no surface. A host holding several clusters names one surface per
/// cluster, each of its own kind, under <c>Clusters</c> (<see cref="OpenAll"/>).
/// </summary>
public static partial class SurfaceChoice
{
    /// <summary>The key naming the surface.</summary>
    public const string SurfaceKey = Table + ":" + SurfaceLeaf;

    /// <summary>The key naming the snapshot file, when the surface is one.
    /// One path, one cluster; several clusters are <see cref="ClustersKey"/>'s
    /// (ADR-0052, amendment 2026-11-13).</summary>
    public const string SnapshotKey = Table + ":" + SnapshotLeaf;

    /// <summary>The word for the surface over a node on this machine.</summary>
    public const string Local = "local";

    /// <summary>The key naming the node's local endpoint, a name or a path,
    /// when the surface is local.</summary>
    public const string LocalKey = Table + ":" + LocalLeaf;

    /// <summary>The word for the surface over a published snapshot.</summary>
    public const string Snapshot = "snapshot";

    /// <summary>The key naming the node's operate address, <c>host:port</c>,
    /// when the surface is remote.</summary>
    public const string AddressKey = Table + ":" + AddressLeaf;

    /// <summary>The word for the surface over a node on another machine.</summary>
    public const string Remote = "remote";

    /// <summary>The key listing one surface per cluster, each a table of its
    /// own naming its <c>Surface</c> and the <c>Local</c>, <c>Address</c> or
    /// <c>Snapshot</c> that word reads: one cluster local at its monitoring
    /// node, another a snapshot — the one way a host names several. It wins
    /// over <c>Surface</c>: a line naming two clusters must not be quietly
    /// replaced by the one the shipped document names.</summary>
    public const string ClustersKey = Table + ":Clusters";

    // The host's table, and the keys every cluster's table shares with it.
    private const string Table = "Xmip";
    private const string SurfaceLeaf = "Surface";
    private const string SnapshotLeaf = "Snapshot";
    private const string LocalLeaf = "Local";
    private const string AddressLeaf = "Address";

    /// <summary>Whether the configuration names a surface at all. A host
    /// whose line may name none — a command, a cmdlet, the prompt — asks this
    /// before <see cref="Open"/>, so that nothing configured is said as such
    /// rather than refused.</summary>
    public static bool IsChosen(IConfiguration configuration)
    {
        return !string.IsNullOrWhiteSpace(configuration[SurfaceKey])
            || Clusters(configuration).Length > 0;
    }

    /// <summary>Open the surface the configuration names. Relative paths
    /// resolve against <paramref name="basePath"/>, the directory the
    /// configuration's paths are written from. A remote surface presents a
    /// key <paramref name="keeper"/> holds where one is given and the
    /// configuration names a certificate and no private key
    /// (<see cref="SecureSurface.Kept"/>).</summary>
    /// <exception cref="InvalidOperationException">The configuration names no
    /// surface, a surface this build does not know, or a snapshot with no
    /// path.</exception>
    public static IOperatorSurface Open(
        IConfiguration configuration, string basePath, IOperatorKeeper? keeper = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        // Found for every surface: each calls the runtime's rules, and the
        // gRPC client, in this library.
        _ = RuntimeLibrary.Find(configuration, basePath);

        return Opened(configuration.GetSection(Table), configuration, basePath, keeper);
    }

    // The one surface a table names — [Xmip] itself, or one cluster of its
    // Clusters — refused in the table's own keys. What a remote presents and
    // trusts is the host's, read from the document: one host, one identity.
    private static IOperatorSurface Opened(
        IConfigurationSection table, IConfiguration document, string basePath,
        IOperatorKeeper? keeper)
    {
        string chosen = table[SurfaceLeaf]?.Trim() ?? string.Empty;
        string surfaceKey = $"{table.Path}:{SurfaceLeaf}";

        // A local surface naming no endpoint names no node, and says so on
        // every answer (LocalOperator.Unnamed), as a command naming none does.
        if (string.Equals(chosen, Local, StringComparison.OrdinalIgnoreCase))
        {
            return new LocalOperator(table[LocalLeaf]);
        }

        if (string.Equals(chosen, Snapshot, StringComparison.OrdinalIgnoreCase))
        {
            string? path = table[SnapshotLeaf];

            return string.IsNullOrWhiteSpace(path)
                ? throw new InvalidOperationException(
                    $"{surfaceKey} is \"{Snapshot}\" and {table.Path}:{SnapshotLeaf} " +
                    "names no file")
                : new SnapshotOperator(TomlDocument.Resolve(path, basePath));
        }

        if (string.Equals(chosen, Remote, StringComparison.OrdinalIgnoreCase))
        {
            string? address = table[AddressLeaf];

            return RemoteOperator.Refusal(address) is null
                ? new RemoteOperator(address!, SecureSurface.From(document, basePath, keeper))
                : throw new InvalidOperationException(
                    $"{surfaceKey} is \"{Remote}\" and {table.Path}:{AddressLeaf} names no node");
        }

        string words = $"\"{Local}\", \"{Snapshot}\" or \"{Remote}\"";

        throw new InvalidOperationException(
            chosen.Length == 0
                ? $"{surfaceKey} is not set; it is {words}"
                : $"{surfaceKey} is \"{chosen}\"; it is {words}");
    }

    /// <summary>
    /// Every cluster the configuration names, as one set a face navigates
    /// (ADR-0052, amendments 2026-09-20 and 2026-11-13). <c>Clusters</c> names
    /// one surface per cluster, each of its own kind —
    /// <c>Clusters = [{ Surface = "local", Local = "xmip-&lt;node&gt;" },
    /// { Surface = "snapshot", Snapshot = "…" }]</c>, or
    /// <c>--Xmip:Clusters:0:Surface=local --Xmip:Clusters:0:Local=…</c> on a
    /// line — each opened by <see cref="Open"/>'s one rule; a document naming
    /// no <c>Clusters</c> names the one surface <see cref="Open"/> returns,
    /// held as a set of one, so a face reads the same shape whatever it was
    /// given. Several are followed for as long as the set lives
    /// (<see cref="ClusterSurfaces.Follow"/>): a face names every cluster on
    /// every render, and each publication is read once as it changes rather
    /// than by whichever render asks first. <paramref name="keeper"/> is as
    /// <see cref="Open"/> takes it.
    /// </summary>
    /// <exception cref="InvalidOperationException">What <see cref="Open"/>
    /// refuses, of any cluster, or two surfaces publishing one cluster; every
    /// surface opened before it is let go.</exception>
    public static ClusterSurfaces OpenAll(
        IConfiguration configuration, string basePath, IOperatorKeeper? keeper = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        IConfigurationSection[] clusters = Clusters(configuration);

        if (clusters.Length == 0)
        {
            return ClusterSurfaces.Over(Open(configuration, basePath, keeper));
        }

        _ = RuntimeLibrary.Find(configuration, basePath);

        return Held(clusters.Select<IConfigurationSection, Func<IOperatorSurface>>(
            table => () => Opened(table, configuration, basePath, keeper)));
    }

    /// <summary>
    /// Where each cluster the configuration names is read, as its surface word
    /// says: the endpoint, the address or the snapshot each of its
    /// <c>Clusters</c> names, else the one <c>[Xmip]</c> itself names. Empty
    /// where it names none. What a host declares it reads (ADR-0053).
    /// </summary>
    public static IReadOnlyList<string> Places(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        IConfigurationSection[] clusters = Clusters(configuration);

        if (clusters.Length > 0)
        {
            return [.. clusters.Select(table => Place(table) ?? table.Path)];
        }

        string? one = Place(configuration.GetSection(Table));

        return string.IsNullOrWhiteSpace(one) ? [] : [one];
    }

    // Where one table's surface reads, as its Surface word says; null where
    // the word names no surface or the table no place for it.
    private static string? Place(IConfigurationSection table)
    {
        string chosen = table[SurfaceLeaf]?.Trim() ?? string.Empty;

        return string.Equals(chosen, Local, StringComparison.OrdinalIgnoreCase)
            ? table[LocalLeaf]
            : string.Equals(chosen, Remote, StringComparison.OrdinalIgnoreCase)
                ? table[AddressLeaf]
                : string.Equals(chosen, Snapshot, StringComparison.OrdinalIgnoreCase)
                    ? table[SnapshotLeaf]
                    : null;
    }

    // Each surface opened in turn and held as one set, followed; where one
    // is refused, or two name one cluster, every one opened is let go.
    private static ClusterSurfaces Held(IEnumerable<Func<IOperatorSurface>> opening)
    {
        List<IOperatorSurface> opened = [];

        try
        {
            foreach (Func<IOperatorSurface> open in opening)
            {
                opened.Add(open());
            }

            return ClusterSurfaces.Over(opened).Follow();
        }
        catch (Exception refused) when (refused is InvalidOperationException
            or ArgumentException)
        {
            foreach (IOperatorSurface surface in opened)
            {
                (surface as IDisposable)?.Dispose();
            }

            throw;
        }
    }

    // The tables of Clusters, in the order they are named.
    private static IConfigurationSection[] Clusters(IConfiguration configuration)
    {
        return [.. configuration.GetSection(ClustersKey).GetChildren()];
    }

    /// <summary>
    /// The one surface a host reads where it reads one: the document's choice,
    /// the first of its <c>Clusters</c> where it names several. A command and a
    /// prompt answer one publication, because a rollup or a sum over two
    /// clusters would be a figure at a scope that is in neither tree (ADR-0052,
    /// amendment 2026-09-20).
    /// </summary>
    /// <exception cref="InvalidOperationException">What <see cref="Open"/>
    /// refuses.</exception>
    public static IOperatorSurface OpenFirst(IConfiguration configuration, string basePath)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        IConfigurationSection[] clusters = Clusters(configuration);

        if (clusters.Length == 0)
        {
            return Open(configuration, basePath);
        }

        _ = RuntimeLibrary.Find(configuration, basePath);
        return Opened(clusters[0], configuration, basePath, keeper: null);
    }
}
