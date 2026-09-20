namespace Xmip.Surface;

/// <summary>
/// The surfaces a face can hold at once, one per cluster. ADR-0052's
/// amendment of 2026-09-14 left *one page navigating between them* queued;
/// this is what a face navigates over. Each publication keeps its own
/// <see cref="IOperatorSurface"/> — a cluster is a whole scope tree with its
/// own root (ADR-0027) — and nothing here adds two of them together: a rollup
/// or a sum over several clusters would be a figure at a scope that is not in
/// any tree, which the amendment of 2026-09-19 forbids for the same reason it
/// forbids one for a pattern.
/// </summary>
/// <remarks>
/// A cluster is named once, when the set is opened, from what its publisher
/// says — <c>[run].cluster</c>, else the one first segment every published
/// scope shares. Kept, not re-read: a roll that ends leaves its file gone and
/// its publication empty, and a cluster that disappeared from the chooser
/// under the operator's hand would be worse than one that says it has stopped
/// publishing. Two surfaces naming one cluster are REFUSED, because
/// <c>Start-XmipTest</c> refuses a cluster already rolling (ADR-0052,
/// amendment 2026-09-19) and a face that showed two of them could not say
/// which it was showing.
/// </remarks>
public sealed class ClusterSurfaces : IDisposable
{
    private readonly IOperatorSurface[] surfaces;

    private readonly string[] clusters;

    private ClusterSurfaces(string[] clusters, IOperatorSurface[] surfaces)
    {
        this.clusters = clusters;
        this.surfaces = surfaces;
    }

    /// <summary>The clusters this face holds, in the order it was given them.
    /// A publication that names no cluster is an empty name, which only ever
    /// happens where there is one, and a face with one shows no chooser.</summary>
    public IReadOnlyList<string> Clusters => clusters;

    /// <summary>How many clusters are held.</summary>
    public int Count => surfaces.Length;

    /// <summary>Whether there is more than one to move between — what a face
    /// asks before it draws a chooser at all.</summary>
    public bool Several => surfaces.Length > 1;

    /// <summary>The one a face shows when nothing said which: the first given.
    /// A set is never empty, so this always answers.</summary>
    public IOperatorSurface First => surfaces[0];

    /// <summary>Where the records come from, for a process that declares
    /// itself (ADR-0053) and for anything that wants one line.</summary>
    public string Source => Several
        ? $"{Count} clusters — {string.Join(", ", clusters)}"
        : First.Source;

    /// <summary>One surface, held as a set of one. Every face reads a set, so
    /// a host over a single snapshot and a host over two are the same face.</summary>
    public static ClusterSurfaces Over(IOperatorSurface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);

        return new ClusterSurfaces([NameOf(surface)], [surface]);
    }

    /// <summary>
    /// A set over several surfaces, each named by its own publication.
    /// </summary>
    /// <exception cref="ArgumentException">No surface at all: a face with
    /// nothing to read is not a set, and whoever opened it says so instead.</exception>
    /// <exception cref="InvalidOperationException">Two of them name one
    /// cluster. The message begins REFUSED and names the cluster.</exception>
    public static ClusterSurfaces Over(IEnumerable<IOperatorSurface> surfaces)
    {
        ArgumentNullException.ThrowIfNull(surfaces);

        IOperatorSurface[] held = [.. surfaces];

        if (held.Length == 0)
        {
            throw new ArgumentException(
                "REFUSED. A surface set names no surface at all.", nameof(surfaces));
        }

        string[] named = [.. held.Select(NameOf)];

        for (int later = 1; later < named.Length; later++)
        {
            int first = Array.IndexOf(named, named[later]);

            if (first < later)
            {
                throw new InvalidOperationException(Twice(named[later], held[first], held[later]));
            }
        }

        return new ClusterSurfaces(named, held);
    }

    /// <summary>
    /// What a surface's publisher calls its cluster: <c>[run].cluster</c>
    /// where it says one, else the one first segment every published scope
    /// shares, else nothing. Read from the publication and never from the file
    /// name — the surface is stated, and what it holds is its own to say
    /// (ADR-0052 clause 3).
    /// </summary>
    public static string NameOf(IOperatorSurface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);

        string said = surface.Run().Cluster;

        if (said.Length > 0)
        {
            return said;
        }

        string[] roots =
        [
            .. surface.Index().Health(ScopeTree.Root)
                .Select(record => ScopeTree.Segment(record.Scope, 0))
                .Where(segment => segment.Length > 0)
                .Distinct(StringComparer.Ordinal),
        ];

        return roots.Length == 1 ? roots[0] : string.Empty;
    }

    /// <summary>Whether this set holds a cluster by that name.</summary>
    public bool Holds(string? cluster)
    {
        return cluster is not null && Array.IndexOf(clusters, cluster) >= 0;
    }

    /// <summary>
    /// The surface for one cluster; <see cref="First"/> for a name this set
    /// does not hold, including none at all. A face that asked for a cluster
    /// that ended sees the first rather than an error page, and
    /// <see cref="Holds"/> is how it knows which it got.
    /// </summary>
    public IOperatorSurface For(string? cluster)
    {
        int held = cluster is null ? -1 : Array.IndexOf(clusters, cluster);

        return held < 0 ? First : surfaces[held];
    }

    /// <summary>
    /// Which cluster a face is on, having asked for this one: the name where
    /// this set holds it, the first otherwise. The name a chooser marks and a
    /// link carries — <see cref="For"/> answers with the same surface.
    /// </summary>
    public string Showing(string? cluster)
    {
        return Holds(cluster) ? cluster! : clusters[0];
    }

    /// <summary>
    /// Whether a cluster's publisher is still saying anything. A roll that
    /// ended leaves the name in the chooser and nothing behind it, and a face
    /// says that rather than dropping the cluster mid-click.
    /// </summary>
    public bool Publishing(string cluster)
    {
        return Holds(cluster) && For(cluster).Index().Leaves > 0;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (IOperatorSurface surface in surfaces)
        {
            (surface as IDisposable)?.Dispose();
        }
    }

    private static string Twice(string cluster, IOperatorSurface first, IOperatorSurface later)
    {
        string named = cluster.Length == 0 ? "no cluster" : $"cluster {cluster}";

        return $"REFUSED. Two surfaces name {named}: {first.Source} and {later.Source}. " +
            "A cluster rolls once (ADR-0052, amendment 2026-09-19).";
    }
}
