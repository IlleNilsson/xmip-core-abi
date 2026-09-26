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

    private readonly Dictionary<string, Entry> entries;

    private readonly Dictionary<string, HealthRecord> stages;

    private readonly Dictionary<string, NodeCapability> declared;

    private ScopeIndex(
        Dictionary<string, Entry> entries,
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
        entries.TryGetValue(ScopeTree.Root, out Entry? root) ? root.Leaves.Count : 0;

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
    public static ScopeIndex Build(
        IEnumerable<HealthRecord> records, IEnumerable<Count> counts, ulong revision, string source)
    {
        Dictionary<string, Entry> entries = new(StringComparer.Ordinal);
        Dictionary<string, HealthRecord> stages = new(StringComparer.Ordinal);
        Dictionary<string, NodeCapability> declared = new(StringComparer.Ordinal);
        DateTimeOffset? observed = null;

        // The whole publication in the runtime's worst-first order, asked once:
        // every "which is worse" below is a lookup of where the runtime put a
        // record, never an order written here (observe::Standing).
        HealthRecord[] published = [.. records];
        Dictionary<HealthRecord, int> rank = new(ReferenceEqualityComparer.Instance);
        int[] order = RuntimeLibrary.Rules.WorstFirst(published);

        for (int at = 0; at < order.Length; at++)
        {
            rank[published[order[at]]] = at;
        }

        HealthRecord? Worse(HealthRecord? now, HealthRecord candidate)
        {
            return now is null || rank[candidate] < rank[now] ? candidate : now;
        }

        Entry root = Reach(entries, ScopeTree.Root, ScopeTree.Root, "cluster");

        foreach (HealthRecord record in published)
        {
            string[] parts = ScopeTree.Parts(record.Scope);

            if (NodeCapability.Declared(record.Scope, record.Evidence) is { Said: true } said)
            {
                declared[said.Node] = said;
            }

            Entry entry = root;
            entry.Leaves.Add(record);
            entry.Worst = Worse(entry.Worst, record);

            foreach ((string scope, string label) in Ancestry(parts))
            {
                entry = Reach(entries, scope, entry.Scope, label);
                entry.Leaves.Add(record);
                entry.Worst = Worse(entry.Worst, record);
            }

            // The stage a record is on is observe's reading, so a node called
            // send is no stage (ScopeTree.Stage, open problem 25, row q).
            string stage = ScopeTree.Stage(record.Scope);

            if (stage.Length > 0)
            {
                stages[stage] = Worse(
                    stages.TryGetValue(stage, out HealthRecord? held) ? held : null, record)!;
            }

            entry.Own = record;
        }

        foreach (Count count in counts)
        {
            int kind = Array.IndexOf(Kinds, count.Counted);

            if (kind < 0)
            {
                continue;
            }

            root.Sums[kind] = (root.Sums[kind] ?? 0) + count.Value;

            foreach ((string scope, string label) in Ancestry(ScopeTree.Parts(count.Scope)))
            {
                Entry entry = Reach(entries, scope, ScopeTree.Parent(scope), label);
                entry.Sums[kind] = (entry.Sums[kind] ?? 0) + count.Value;
            }

            if (count.Observed is { } seen && (observed is null || seen > observed))
            {
                observed = seen;
            }
        }

        foreach (Entry entry in entries.Values)
        {
            if (entry.Children.Count > 1)
            {
                List<Entry> ordered = [.. ScopeTree.WorstFirst(entry.Children, Standing)];
                entry.Children.Clear();
                entry.Children.AddRange(ordered);
            }

            entry.Rank = rank;
        }

        return new ScopeIndex(entries, stages, declared, revision, source, observed);
    }

    /// <summary>
    /// What a node declared it can do, as the node itself published it at
    /// <c>&lt;node&gt;/capability</c> (ADR-0056 clause 1: a node declares its
    /// capabilities and nothing is inferred). <see cref="NodeCapability.None"/>
    /// when this publication carries no such record for the node — which is
    /// not the same as a node that declared no stage, and the two never read
    /// alike.
    /// </summary>
    public NodeCapability Capability(string node)
    {
        return declared.TryGetValue(node, out NodeCapability? said) ? said : NodeCapability.None;
    }

    /// <summary>Every node that published what it can do, by name.</summary>
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
        return entries.TryGetValue(Normal(scope), out Entry? entry) ? entry.Sorted() : [];
    }

    /// <summary>The record at exactly this scope: a Location's own verdict.</summary>
    public HealthRecord? Own(string scope)
    {
        return entries.TryGetValue(Normal(scope), out Entry? entry) ? entry.Own : null;
    }

    /// <summary>The worst leaf at or beneath a scope.</summary>
    public HealthRecord? Worst(string scope)
    {
        return entries.TryGetValue(Normal(scope), out Entry? entry) ? entry.Worst : null;
    }

    /// <summary>The rolled-up mood at a scope (ADR-0041): a leaf's own mood, a
    /// parent's Fine or Holding. Null when nothing is recorded there.</summary>
    public HealthState? Rollup(string scope)
    {
        return entries.TryGetValue(Normal(scope), out Entry? entry) ? entry.Mood : null;
    }

    /// <summary>The six figures at a scope, summed over everything beneath.</summary>
    public Figures Figures(string scope)
    {
        return entries.TryGetValue(Normal(scope), out Entry? entry)
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

        if (kind < 0 || !entries.TryGetValue(Normal(scope), out Entry? entry)
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
        return entries.TryGetValue(Normal(scope), out Entry? entry)
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
        return entries.TryGetValue(ScopeTree.Root, out Entry? root)
            ? [.. root.Children.Select(child => child.Label).Order(StringComparer.Ordinal)]
            : [];
    }

    private static string Normal(string scope)
    {
        return scope == ScopeTree.Root ? scope : ScopeTree.Join(ScopeTree.Parts(scope));
    }

    private static Entry Reach(
        Dictionary<string, Entry> entries, string scope, string parent, string label)
    {
        if (entries.TryGetValue(scope, out Entry? entry))
        {
            return entry;
        }

        entry = new Entry(scope, label);
        entries[scope] = entry;

        if (scope != ScopeTree.Root && entries.TryGetValue(parent, out Entry? above))
        {
            above.Children.Add(entry);

            // Where a stage begins: on one, beneath one on none (ScopeTree.Stage).
            entry.Stage = ScopeTree.Stage(scope);
            entry.BeginsStage = entry.Stage.Length > 0 && above.Stage.Length == 0;
        }

        return entry;
    }

    /// <summary>Every scope from the first segment down to the scope itself,
    /// each with its label.</summary>
    private static IEnumerable<(string Scope, string Label)> Ancestry(string[] parts)
    {
        string built = ScopeTree.Root;

        for (int depth = 0; depth < parts.Length; depth++)
        {
            built = depth == 0 ? built + parts[0] : built + "/" + parts[depth];
            yield return (built, parts[depth]);
        }
    }

    /// <summary>An entry as the record it stands as in the worst-first order:
    /// its worst leaf's mood and severity under its own label, and an entry
    /// with nothing beneath it as a Fine one.</summary>
    private static HealthRecord Standing(Entry entry)
    {
        return new HealthRecord(
            entry.Label,
            entry.Worst?.State ?? HealthState.Fine,
            entry.Worst?.Severity ?? 0,
            string.Empty,
            default);
    }

    /// <summary>One scope in the tree and everything the tree knows about it.</summary>
    private sealed class Entry(string scope, string label)
    {
        private IReadOnlyList<HealthRecord>? sorted;

        public string Scope { get; } = scope;

        public string Label { get; } = label;

        public string Stage { get; set; } = string.Empty;
        public bool BeginsStage { get; set; }

        public List<HealthRecord> Leaves { get; } = [];

        public List<Entry> Children { get; } = [];

        public HealthRecord? Worst { get; set; }

        public HealthRecord? Own { get; set; }

        /// <summary>Where the runtime put each record of the publication in
        /// its worst-first order.</summary>
        public Dictionary<HealthRecord, int> Rank { get; set; } = [];

        public ulong?[] Sums { get; } = new ulong?[6];

        public bool IsLeaf => Children.Count == 0;

        public HealthState? Mood =>
            Worst is null ? null : IsLeaf ? Worst.State : ScopeTree.Rolled(Worst.State);

        public Branch Branch => new(
            Scope,
            Label,
            Mood ?? HealthState.Fine,
            IsLeaf,
            Leaves.Count,
            Worst ?? new HealthRecord(
                Scope, HealthState.Fine, 0, string.Empty, DateTimeOffset.MinValue));

        public IReadOnlyList<HealthRecord> Sorted()
        {
            sorted ??= [.. Leaves.OrderBy(leaf => Rank[leaf])];

            return sorted;
        }
    }
}
