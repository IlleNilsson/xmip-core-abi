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

    private ScopeIndex(
        Dictionary<string, Entry> entries,
        Dictionary<string, HealthRecord> stages,
        ulong revision,
        string source,
        DateTimeOffset? observed)
    {
        this.entries = entries;
        this.stages = stages;
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
        Entry root = Reach(entries, ScopeTree.Root, ScopeTree.Root, "cluster");
        DateTimeOffset? observed = null;

        foreach (HealthRecord record in records)
        {
            Entry entry = root;
            entry.Leaves.Add(record);
            entry.Worst = Worse(entry.Worst, record);

            foreach ((string scope, string label) in Ancestry(record.Scope))
            {
                entry = Reach(entries, scope, entry.Scope, label);
                entry.Leaves.Add(record);
                entry.Worst = Worse(entry.Worst, record);

                if (ScopeTree.Stages.Contains(label))
                {
                    stages[label] = Worse(
                        stages.TryGetValue(label, out HealthRecord? held) ? held : null, record)!;
                }
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

            foreach ((string scope, string label) in Ancestry(count.Scope))
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
            entry.Children.Sort(ByTrouble);
        }

        return new ScopeIndex(entries, stages, revision, source, observed);
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
        }

        return entry;
    }

    /// <summary>Every scope from the first segment down to the scope itself,
    /// each with its label.</summary>
    private static IEnumerable<(string Scope, string Label)> Ancestry(string scope)
    {
        string[] parts = ScopeTree.Parts(scope);
        string built = ScopeTree.Root;

        for (int depth = 0; depth < parts.Length; depth++)
        {
            built = depth == 0 ? built + parts[0] : built + "/" + parts[depth];
            yield return (built, parts[depth]);
        }
    }

    private static HealthRecord? Worse(HealthRecord? held, HealthRecord candidate)
    {
        return held is null || Compare(candidate, held) < 0 ? candidate : held;
    }

    /// <summary>Worst first: mood, then severity, then scope.</summary>
    private static int Compare(HealthRecord a, HealthRecord b)
    {
        int byState = b.State.CompareTo(a.State);

        if (byState != 0)
        {
            return byState;
        }

        int bySeverity = b.Severity.CompareTo(a.Severity);

        return bySeverity != 0 ? bySeverity : string.CompareOrdinal(a.Scope, b.Scope);
    }

    private static int ByTrouble(Entry a, Entry b)
    {
        HealthState worstA = a.Worst?.State ?? HealthState.Fine;
        HealthState worstB = b.Worst?.State ?? HealthState.Fine;
        int byState = worstB.CompareTo(worstA);

        if (byState != 0)
        {
            return byState;
        }

        int bySeverity = (b.Worst?.Severity ?? 0).CompareTo(a.Worst?.Severity ?? 0);

        return bySeverity != 0 ? bySeverity : string.CompareOrdinal(a.Label, b.Label);
    }

    /// <summary>One scope in the tree and everything the tree knows about it.</summary>
    private sealed class Entry(string scope, string label)
    {
        private IReadOnlyList<HealthRecord>? sorted;

        public string Scope { get; } = scope;

        public string Label { get; } = label;

        public List<HealthRecord> Leaves { get; } = [];

        public List<Entry> Children { get; } = [];

        public HealthRecord? Worst { get; set; }

        public HealthRecord? Own { get; set; }

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
            if (sorted is null)
            {
                HealthRecord[] copy = [.. Leaves];
                Array.Sort(copy, Compare);
                sorted = copy;
            }

            return sorted;
        }
    }
}
