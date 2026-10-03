using Xmip.Abi.Operate;

namespace Xmip.Surface;

/// <summary>
/// One publication as the scope tree it is (ADR-0027): every scope with its
/// worst leaf, its rollup, the leaves beneath it, its children worst first
/// and its six figures, all computed in one pass when the publication is
/// read. A surface answers from this by lookup and never filters the
/// records again (ADR-0052, amendment 2026-09-15: the index). Immutable; a
/// new publication is a new index, and the change feed says when.
/// </summary>
public sealed class ScopeIndex
{
    private static readonly Counted[] Kinds =
    [
        Counted.Streams, Counted.Messages, Counted.Journeys,
        Counted.Bytes, Counted.Retrying, Counted.Failed,
    ];

    private readonly Dictionary<string, ScopeEntry> entries;

    private readonly Dictionary<string, HealthRecord> stages;

    private readonly Dictionary<string, NodeCapability> declared;

    private ScopeIndex(
        Dictionary<string, ScopeEntry> entries,
        Dictionary<string, HealthRecord> stages,
        Dictionary<string, NodeCapability> declared,
        ulong revision,
        string source,
        DateTimeOffset? observed)
    {
        this.entries = entries;
        this.stages = stages;
        this.declared = declared;
        Revision = revision;
        Source = source;
        Observed = observed;
    }

    /// <summary>The publication this index is, as the change feed numbers it.</summary>
    public ulong Revision { get; }

    /// <summary>Where the publication came from.</summary>
    public string Source { get; }

    /// <summary>When the newest figure was observed, if any was.</summary>
    public DateTimeOffset? Observed { get; }

    /// <summary>How many leaves the publication holds.</summary>
    public int Leaves =>
        entries.TryGetValue(ScopeTree.Root, out ScopeEntry? root) ? root.Leaves.Count : 0;

    /// <summary>How many scopes it holds altogether, the branches and the root
    /// included — what a pattern is matched against.</summary>
    public int Scopes => entries.Count;

    /// <summary>A count at a scope, as a publication states it: the sum beneath
    /// the scope is the index's to compute.</summary>
    public sealed record Count(
        string Scope, Counted Counted, ulong Value, DateTimeOffset? Observed);

    /// <summary>An index over nothing.</summary>
    public static ScopeIndex Empty(string source)
    {
        return Build([], [], 0, source);
    }

    /// <summary>Build the tree from what a publication holds, in one pass.</summary>
    /// <remarks>The records are walked in the runtime's worst-first order, so
    /// a scope's first leaf is its worst and its leaves come in the order
    /// <see cref="Health"/> answers in; every scope's children are ordered in
    /// one call (observe::Standing sorts stably). Until 2026-10-03 that was a
    /// map lookup per leaf and a call per scope.</remarks>
    public static ScopeIndex Build(
        IEnumerable<HealthRecord> records, IEnumerable<Count> counts, ulong revision, string source)
    {
        Dictionary<string, ScopeEntry> entries = [with(StringComparer.Ordinal)];
        Dictionary<string, HealthRecord> stages = [with(StringComparer.Ordinal)];
        Dictionary<string, NodeCapability> declared = [with(StringComparer.Ordinal)];
        DateTimeOffset? observed = null;
        HealthRecord[] published = [.. records];
        ScopeEntry root = new(ScopeTree.Root, "cluster", null);
        entries[root.Scope] = root;

        // What a node declared is what its last capability record said.
        foreach (HealthRecord record in published)
        {
            if (NodeCapability.Declared(record.Scope, record.Evidence) is { Said: true } said)
            {
                declared[said.Node] = said;
            }
        }

        // Worst first, as the runtime orders it, asked once (observe::Standing).
        foreach (int at in RuntimeLibrary.Rules.WorstFirst(published))
        {
            HealthRecord record = published[at];
            ScopeEntry entry = root.Holding(record);

            foreach (ScopeEntry beneath in Ancestry(entries, root, ScopeTree.Parts(record.Scope)))
            {
                entry = beneath.Holding(record);
            }

            entry.Owning(record, at);

            // The stage a record is on is observe's reading of its scope, so a
            // node called send is no stage (ScopeTree.Stage, open problem 25,
            // row q); the record's own scope read it when it was reached.
            if (entry.Stage.Length > 0)
            {
                stages.TryAdd(entry.Stage, record);
            }
        }

        foreach (Count count in counts)
        {
            int kind = Array.IndexOf(Kinds, count.Counted);

            if (kind < 0)
            {
                continue;
            }

            root.Sums[kind] = (root.Sums[kind] ?? 0) + count.Value;

            foreach (ScopeEntry entry in Ancestry(entries, root, ScopeTree.Parts(count.Scope)))
            {
                entry.Sums[kind] = (entry.Sums[kind] ?? 0) + count.Value;
            }

            if (count.Observed is { } seen && (observed is null || seen > observed))
            {
                observed = seen;
            }
        }

        Order(entries.Values);

        return new ScopeIndex(entries, stages, declared, revision, source, observed);
    }

    /// <summary>
    /// What a node declared — its roles and its online capability — as the
    /// node itself published it at <c>&lt;node&gt;/capability</c> (ADR-0056
    /// clause 1: a node declares its capabilities and nothing is inferred).
    /// <see cref="NodeCapability.None"/> when this publication carries no
    /// such record for the node — which is not the same as a node that
    /// declared no role, and the two never read alike.
    /// </summary>
    public NodeCapability Capability(string node)
    {
        return declared.TryGetValue(node, out NodeCapability? said) ? said : NodeCapability.None;
    }

    /// <summary>Every node that published what it declares, by name.</summary>
    public IReadOnlyList<NodeCapability> Capabilities()
    {
        return [.. declared.Values.OrderBy(said => said.Node, StringComparer.Ordinal)];
    }

    /// <summary>
    /// Every scope a wildcard names, in ordinal order (ADR-0059 clause 7, read
    /// for a surface that is not PowerShell): the match is
    /// <see cref="ScopePattern"/>, the same one every surface uses, and a
    /// pattern with no wildcard names at most the one scope it spells.
    /// </summary>
    public IReadOnlyList<string> Matching(string pattern)
    {
        return
        [
            .. entries.Keys.Where(scope => ScopePattern.Matches(scope, pattern))
                .OrderBy(scope => scope, StringComparer.Ordinal),
        ];
    }

    /// <summary>Whether the publication says anything at or beneath a scope.</summary>
    public bool Contains(string scope)
    {
        return entries.ContainsKey(Normal(scope));
    }

    /// <summary>Health at and beneath a scope, worst first, most severe first
    /// within a mood, then by scope — the order every surface answers in.</summary>
    public IReadOnlyList<HealthRecord> Health(string scope)
    {
        return entries.TryGetValue(Normal(scope), out ScopeEntry? entry)
            ? entry.Leaves.AsReadOnly()
            : [];
    }

    /// <summary>The record at exactly this scope: a Location's own verdict.</summary>
    public HealthRecord? Own(string scope)
    {
        return entries.TryGetValue(Normal(scope), out ScopeEntry? entry) ? entry.Own : null;
    }

    /// <summary>The worst leaf at or beneath a scope.</summary>
    public HealthRecord? Worst(string scope)
    {
        return entries.TryGetValue(Normal(scope), out ScopeEntry? entry) ? entry.Worst : null;
    }

    /// <summary>The rolled-up mood at a scope (ADR-0041): a leaf's own mood, a
    /// parent's Fine or Holding. Null when nothing is recorded there.</summary>
    public HealthState? Rollup(string scope)
    {
        return entries.TryGetValue(Normal(scope), out ScopeEntry? entry) ? entry.Mood : null;
    }

    /// <summary>The six figures at a scope, summed over everything beneath.</summary>
    public Figures Figures(string scope)
    {
        return entries.TryGetValue(Normal(scope), out ScopeEntry? entry)
            ? new Figures(
                scope,
                entry.Sums[0], entry.Sums[1], entry.Sums[2],
                entry.Sums[3], entry.Sums[4], entry.Sums[5],
                Observed)
            : Surface.Figures.None(scope);
    }

    /// <summary>One count at a scope as a measurement, or null when the
    /// publication has none of that kind beneath it.</summary>
    public MeasurementRecord? Measure(string scope, Counted counted)
    {
        int kind = Array.IndexOf(Kinds, counted);

        if (kind < 0 || !entries.TryGetValue(Normal(scope), out ScopeEntry? entry)
            || entry.Sums[kind] is not { } value)
        {
            return null;
        }

        DateTimeOffset seen = Observed ?? DateTimeOffset.UtcNow;

        return new MeasurementRecord(scope, counted, value, seen.AddMinutes(-1), seen, seen);
    }

    /// <summary>What sits directly beneath a scope, worst first: a child with
    /// nothing deeper is a leaf with its own mood; anything deeper rolls up.</summary>
    public IReadOnlyList<Branch> Branches(string scope)
    {
        return entries.TryGetValue(Normal(scope), out ScopeEntry? entry)
            ? [.. entry.Children.Select(child => child.Branch)]
            : [];
    }

    /// <summary>Every scope where a stage begins, ordinal: each node's
    /// <c>receive</c> for <c>receive</c>, what a stage card sums.</summary>
    public IReadOnlyList<string> StageScopes(string stage)
    {
        return [.. entries.Values.Where(entry => entry.BeginsStage && entry.Stage == stage)
            .Select(entry => entry.Scope).Order(StringComparer.Ordinal)];
    }

    /// <summary>The worst leaf on a stage of the message path — receive, process
    /// or send — wherever that stage sits in the tree. Null when no leaf is
    /// on it.</summary>
    public HealthRecord? WorstAtStage(string stage)
    {
        return stages.TryGetValue(stage, out HealthRecord? worst) ? worst : null;
    }

    /// <summary>The first segment beneath the root of every scope: the nodes.</summary>
    public IReadOnlyList<string> Nodes()
    {
        return entries.TryGetValue(ScopeTree.Root, out ScopeEntry? root)
            ? [.. root.Children.Select(child => child.Label).Order(StringComparer.Ordinal)]
            : [];
    }

    private static string Normal(string scope)
    {
        return scope == ScopeTree.Root ? scope : ScopeTree.Join(ScopeTree.Parts(scope));
    }

    /// <summary>Every scope from the first segment down to the scope itself,
    /// made beneath the one above it the first time; a string only when new.</summary>
    private static List<ScopeEntry> Ancestry(
        Dictionary<string, ScopeEntry> entries, ScopeEntry root, string[] parts)
    {
        Dictionary<string, ScopeEntry>.AlternateLookup<ReadOnlySpan<char>> lookup =
            entries.GetAlternateLookup<ReadOnlySpan<char>>();
        int length = ScopeTree.Root.Length + parts.Sum(part => part.Length + 1);
        char[] built = new char[length];
        ScopeTree.Root.CopyTo(built);
        int end = ScopeTree.Root.Length;
        List<ScopeEntry> reached = [with(parts.Length)];
        ScopeEntry above = root;

        for (int depth = 0; depth < parts.Length; depth++)
        {
            if (depth > 0)
            {
                built[end++] = '/';
            }

            parts[depth].CopyTo(built.AsSpan(end));
            end += parts[depth].Length;
            ReadOnlySpan<char> scope = built.AsSpan(0, end);

            if (!lookup.TryGetValue(scope, out ScopeEntry? entry))
            {
                entry = new ScopeEntry(scope.ToString(), parts[depth], above);
                entries[entry.Scope] = entry;
            }

            reached.Add(entry);
            above = entry;
        }

        return reached;
    }

    /// <summary>Every scope's children worst first, in one call to the
    /// runtime for all of them.</summary>
    private static void Order(IEnumerable<ScopeEntry> all)
    {
        ScopeEntry[] parents = [.. all.Where(entry => entry.Children.Count > 1)];
        ScopeEntry[] children = [.. parents.SelectMany(entry => entry.Children)];

        foreach (ScopeEntry parent in parents)
        {
            parent.Children.Clear();
        }

        foreach (ScopeEntry child in ScopeTree.WorstFirst(children, child => child.Standing))
        {
            child.Parent!.Children.Add(child);
        }
    }
}
