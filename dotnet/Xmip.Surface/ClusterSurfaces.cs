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
///
/// A cluster whose run declared itself hidden — an assistant's test run
/// (the owner, 2026-09-29; ADR-0028 and ADR-0052, amendments 2026-09-30) —
/// is held like any other and listed only to a face that asks to include what
/// is hidden: every question a face asks of the set says whether it does, and
/// the answer is the one rule, <c>observe::run::shown</c>, over what the run
/// declared, read once with its name. Nothing is read out of the name.
/// </remarks>
public sealed class ClusterSurfaces : IDisposable
{
    private readonly IOperatorSurface[] surfaces;

    private readonly string[] clusters;

    private readonly bool[] hidden;

    // Ends every follow this set started, when the set is disposed.
    private readonly CancellationTokenSource following = new();

    private ClusterSurfaces(string[] clusters, IOperatorSurface[] surfaces)
    {
        this.clusters = clusters;
        this.surfaces = surfaces;
        hidden = [.. surfaces.Select(surface => surface.Run().Hidden)];
    }

    /// <summary>Every cluster this face holds, hidden or not, in the order it
    /// was given them. A publication that names no cluster is an empty name,
    /// which only ever happens where there is one.</summary>
    public IReadOnlyList<string> Clusters => clusters;

    /// <summary>How many clusters are held.</summary>
    public int Count => surfaces.Length;

    /// <summary>The clusters a face lists: every one whose run declared
    /// nothing, and the hidden ones too where it is
    /// <paramref name="includingHidden"/>.</summary>
    public IReadOnlyList<string> Listed(bool includingHidden)
    {
        return [.. clusters.Where((_, at) => Shown(at, includingHidden))];
    }

    /// <summary>Whether there is more than one listed to move between — what
    /// a face asks before it draws a chooser at all.</summary>
    public bool Several(bool includingHidden)
    {
        return Listed(includingHidden).Count > 1;
    }

    /// <summary>Whether this cluster's run declared itself hidden when it was
    /// started; false for a name this set does not hold.</summary>
    public bool Hidden(string? cluster)
    {
        int held = cluster is null ? -1 : Array.IndexOf(clusters, cluster);

        return held >= 0 && hidden[held];
    }

    /// <summary>Whether any cluster held is hidden — whether a face has
    /// anything for a "show test clusters" choice to show.</summary>
    public bool AnyHidden => hidden.Contains(true);

    /// <summary>The one a face shows when nothing said which: the first given.
    /// A set is never empty, so this always answers.</summary>
    public IOperatorSurface First => surfaces[0];

    /// <summary>Where the records come from, for a process that declares
    /// itself (ADR-0053) and for anything that wants one line.</summary>
    public string Source => Count > 1
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
    /// The surface for one cluster, among those listed: that one where it is
    /// listed, else the first listed — a face that asked for a cluster that
    /// ended, or for a hidden one without including what is hidden, sees the
    /// first rather than an error page, and <see cref="Showing"/> says which
    /// it got. Where nothing is listed — every cluster held is hidden and the
    /// face did not include them — a surface that holds nothing and says why.
    /// </summary>
    public IOperatorSurface For(string? cluster, bool includingHidden)
    {
        int at = Chosen(cluster, includingHidden);

        return at < 0 ? Withheld.Surface : surfaces[at];
    }

    /// <summary>
    /// Which cluster a face is on, having asked for this one: the name where
    /// it is listed, the first listed otherwise, and empty where nothing is.
    /// The name a chooser marks and a link carries — <see cref="For"/>
    /// answers with the same surface.
    /// </summary>
    public string Showing(string? cluster, bool includingHidden)
    {
        int at = Chosen(cluster, includingHidden);

        return at < 0 ? string.Empty : clusters[at];
    }

    /// <summary>
    /// Whether a cluster's publisher is still saying anything. A roll that
    /// ended leaves the name in the chooser and nothing behind it, and a face
    /// says that rather than dropping the cluster mid-click.
    /// </summary>
    public bool Publishing(string cluster)
    {
        int held = Array.IndexOf(clusters, cluster);

        return held >= 0 && surfaces[held].Index().Leaves > 0;
    }

    // The one rule, observe::run::shown, over what the run declared.
    private bool Shown(int at, bool includingHidden)
    {
        return RuntimeLibrary.Rules.Shown(hidden[at], includingHidden);
    }

    // The index a face is on: the asked cluster where it is listed, else the
    // first listed, else none.
    private int Chosen(string? cluster, bool includingHidden)
    {
        int asked = cluster is null ? -1 : Array.IndexOf(clusters, cluster);

        if (asked >= 0 && Shown(asked, includingHidden))
        {
            return asked;
        }

        for (int at = 0; at < clusters.Length; at++)
        {
            if (Shown(at, includingHidden))
            {
                return at;
            }
        }

        return -1;
    }

    /// <summary>
    /// Follow every surface held for as long as this set lives: each one's
    /// change feed is read to its end on a thread of its own, so a surface
    /// that reads a file reads each publication once, as it changes, and
    /// every question a face asks of it — a view's, a chooser's naming every
    /// cluster on every render — is answered from what was already read
    /// (<see cref="SnapshotOperator"/>). Until 2026-10-03 a render after a
    /// Playground tick read every changed cluster's 2 MB snapshot itself.
    /// Returns this set.
    /// </summary>
    public ClusterSurfaces Follow()
    {
        foreach (IOperatorSurface surface in surfaces)
        {
            _ = Task.Run(() => Following(surface, following.Token));
        }

        return this;
    }

    private static async Task Following(IOperatorSurface surface, CancellationToken stop)
    {
        try
        {
            await foreach (SurfaceChange _ in surface.WatchAsync(stop).ConfigureAwait(false))
            {
                // Reading the feed is the point: the surface reads before it announces.
            }
        }
        catch (OperationCanceledException)
        {
            // Disposing the set ends its follows.
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        following.Cancel();
        following.Dispose();

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
