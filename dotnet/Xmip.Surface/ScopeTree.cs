using Xmip.Abi.Operate;

namespace Xmip.Surface;

/// <summary>
/// The scope tree, once. A scope is an Xmip URI (ADR-0027 clause 3) and the
/// tree beneath the cluster is <c>node / stage / name</c>; every surface asks
/// here what is beneath what, how a parent rolls up, and which leaf beneath a
/// scope is worst, so the board and the cmdlet cannot answer differently
/// (ADR-0052 clause 1).
/// </summary>
/// <remarks>
/// <para>The rules underneath are not written here. Containment and a scope's parts
/// are <c>observe::Scope</c>'s, the stage words and whether a stage pauses
/// <c>node::Stage</c>'s, what a stage counts <c>observe::Counted</c>'s, and
/// the rollup and the worst-first order <c>observe::Health</c>'s and
/// <c>observe::Standing</c>'s, and this calls each in the runtime's library (<see cref="RuntimeLibrary.Rules"/>, <c>xmip_operate.h</c>
/// section 7) — one implementation, which the snapshot answers by too
/// (ADR-0052, amendment 2026-09-24).</para>
/// <para>ADR-0041: a leaf's mood does not propagate. A parent is <c>Fine</c>
/// when every leaf beneath it is, and <c>Holding</c> the moment one is not;
/// the worst leaf carries the real mood and its evidence, and that is what a
/// Holding scope shows beside the word (ADR-0052 clause 2).</para>
/// </remarks>
public static class ScopeTree
{
    /// <summary>The cluster: the root every scope is beneath.</summary>
    public const string Root = "xmip:///";

    /// <summary>
    /// The path segments of a scope: <c>xmip:///edge-01/receive/orders</c> is
    /// <c>edge-01</c>, <c>receive</c>, <c>orders</c>. The scheme and the
    /// authority go; an omitted host means estate-wide and the tree is the
    /// path. The root has no segments, so everything is beneath it.
    /// <c>observe::Scope::segments</c>, called in the runtime.
    /// </summary>
    public static string[] Parts(string scope)
    {
        return RuntimeLibrary.Rules.Parts(scope);
    }

    /// <summary>A scope built from segments, under the root.</summary>
    public static string Join(IEnumerable<string> parts)
    {
        return Root + string.Join('/', parts);
    }

    /// <summary>Whether <paramref name="candidate"/> is <paramref name="scope"/>
    /// itself or beneath it: the scope's segments are a prefix of the
    /// candidate's. <c>observe::Scope::contains</c>, called in the runtime —
    /// the one rule the snapshot answers by (ADR-0052, amendment
    /// 2026-09-24).</summary>
    public static bool Beneath(string candidate, string scope)
    {
        return RuntimeLibrary.Rules.Contains(scope, candidate);
    }

    /// <summary>The scope one level up; the root's parent is the root.</summary>
    public static string Parent(string scope)
    {
        string[] parts = Parts(scope);

        return parts.Length <= 1 ? Root : Join(parts.Take(parts.Length - 1));
    }

    /// <summary>The first segment: the node a thing runs on, or empty at the
    /// root.</summary>
    public static string Node(string scope)
    {
        return Segment(scope, 0);
    }

    /// <summary>The three stages of the message path, in the order an operator
    /// reads them: <c>node::Stage::WORDS</c>, called in the runtime.</summary>
    public static IReadOnlyList<string> Stages => RuntimeLibrary.Rules.StageWords;

    /// <summary>The stage of the message path a scope sits in — <c>receive</c>,
    /// <c>process</c> or <c>send</c> — wherever that segment falls: directly
    /// under a node (<c>edge-01/receive/orders</c>) or under a test the
    /// Playground nests between (<c>playground/round-trip/receive/tcp/json</c>).
    /// Empty for a scope on no stage.</summary>
    public static string Stage(string scope)
    {
        return Parts(scope).FirstOrDefault(part => Stages.Contains(part)) ?? string.Empty;
    }

    /// <summary>What a stage counts (ADR-0027 clause 5, the three words kept
    /// apart): Streams at Receive, Journeys in Process, Messages at Send —
    /// <c>observe::Counted::at</c>, called in the runtime, so the board's tiles,
    /// the figures and the prompt say what the runtime counts.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Not a stage.</exception>
    public static Counted CountedAt(string stage)
    {
        return RuntimeLibrary.Rules.StageCounted(stage)
            ?? throw new ArgumentOutOfRangeException(
                nameof(stage), stage, "not a stage of the message path");
    }

    /// <summary>Whether an operator may pause what sits at a scope: it is on a
    /// stage, and the stage is one a Location is paused at —
    /// <c>node::Stage::pausable</c>, called in the runtime. A Process runs off
    /// a subscription; an operator pauses the Location that feeds it.</summary>
    public static bool Pausable(string scope)
    {
        string stage = Stage(scope);

        return stage.Length > 0 && RuntimeLibrary.Rules.Pausable(stage) == true;
    }

    /// <summary>What a thing configured at a stage is called — a receive
    /// location, an xmip process, a send location: <c>node::Stage::location</c>,
    /// called in the runtime. Null for a word that is no stage.</summary>
    public static string? Location(string stage)
    {
        return RuntimeLibrary.Rules.Location(stage);
    }

    /// <summary>One segment by position, or empty when the scope is not that
    /// deep.</summary>
    public static string Segment(string scope, int index)
    {
        return Parts(scope).ElementAtOrDefault(index) ?? string.Empty;
    }

    /// <summary>What a thing is called beneath its node and stage — the leaf's
    /// own name, or the stage when there is nothing beneath it, or the scope
    /// itself when it is shallower than that.</summary>
    public static string Name(string scope)
    {
        string[] parts = Parts(scope);

        return parts.Length switch
        {
            >= 3 => string.Join('/', parts.Skip(2)),
            2 => parts[1],
            _ => scope,
        };
    }

    /// <summary>The rollup (ADR-0041): a parent above anything not Fine is
    /// Holding, and only Fine when everything beneath is —
    /// <c>observe::Health::rolled</c>, called in the runtime, the rule the
    /// snapshot rolls up by.</summary>
    public static HealthState Rolled(HealthState worst)
    {
        return RuntimeLibrary.Rules.Rolled(worst);
    }

    /// <summary>The rollup over a set of leaves, or null when there are none —
    /// nothing recorded is not Fine, it is nothing.</summary>
    public static HealthState? Rollup(IEnumerable<HealthRecord> records)
    {
        HealthRecord? worst = Worst(records);

        return worst is null ? null : Rolled(worst.State);
    }

    /// <summary>The worst of a set of leaves: the worst mood, then the highest
    /// severity, then the first scope in ordinal order so that equals answer
    /// the same way every time. Null when there are none.</summary>
    public static HealthRecord? Worst(IEnumerable<HealthRecord> records)
    {
        IReadOnlyList<HealthRecord> ordered = WorstFirst(records);

        return ordered.Count == 0 ? null : ordered[0];
    }

    /// <summary>The worst leaf beneath a scope, with its evidence and when it
    /// was observed — what a Holding scope shows beside the word (ADR-0052
    /// clause 2). Null when nothing is beneath it.</summary>
    public static HealthRecord? WorstLeaf(IEnumerable<HealthRecord> records, string scope)
    {
        return Worst(records.Where(record => Beneath(record.Scope, scope)));
    }

    /// <summary>Leaves ordered worst first, the order every surface returns
    /// health in: the worse mood, then the higher severity, then the scope —
    /// <c>observe::Standing</c>, asked of the runtime once for the lot, so a
    /// surface orders what it holds as the runtime orders what it
    /// publishes.</summary>
    public static IReadOnlyList<HealthRecord> WorstFirst(IEnumerable<HealthRecord> records)
    {
        return WorstFirst([.. records], record => record);
    }

    /// <summary>Things in the worst-first order, each standing as the record
    /// <paramref name="standsAs"/> gives it — a branch as its worst leaf's mood
    /// and severity under its own label. One call to the runtime.</summary>
    public static IReadOnlyList<T> WorstFirst<T>(
        IReadOnlyList<T> items, Func<T, HealthRecord> standsAs)
    {
        ArgumentNullException.ThrowIfNull(items);

        HealthRecord[] standing = [.. items.Select(standsAs)];

        return [.. RuntimeLibrary.Rules.WorstFirst(standing).Select(at => items[at])];
    }

    /// <summary>The path from the cluster down to a scope, each step a place to
    /// climb back to.</summary>
    public static IReadOnlyList<Crumb> Trail(string scope)
    {
        List<Crumb> trail = [new Crumb("cluster", Root)];
        List<string> accumulated = [];

        foreach (string segment in Parts(scope))
        {
            accumulated.Add(segment);
            trail.Add(new Crumb(segment, Join(accumulated)));
        }

        return trail;
    }

    /// <summary>
    /// What sits directly beneath a scope: every leaf beneath it grouped by its
    /// next segment, each group carrying its worst leaf, worst group first. A
    /// group whose members are all exactly one level down is a leaf and shows
    /// its own mood; anything deeper is a parent and rolls up.
    /// </summary>
    public static IReadOnlyList<Branch> Branches(IEnumerable<HealthRecord> records, string scope)
    {
        int depth = Parts(scope).Length;

        IEnumerable<Branch> branches = records
            .Where(record => Beneath(record.Scope, scope))
            .GroupBy(record => Join(Parts(record.Scope).Take(depth + 1)), StringComparer.Ordinal)
            .Select(group =>
            {
                HealthRecord worst = Worst(group)!;
                bool isLeaf = group.All(record => Parts(record.Scope).Length == depth + 1);

                return new Branch(
                    group.Key,
                    Parts(group.Key).Last(),
                    isLeaf ? worst.State : Rolled(worst.State),
                    isLeaf,
                    group.Count(),
                    worst);
            });

        // The worst-first order over each branch's worst leaf, told apart by
        // the branch's label where two stand together.
        return WorstFirst([.. branches], Standing);
    }

    /// <summary>A branch as the record it stands as in the worst-first order:
    /// its worst leaf's mood and severity, under its own label.</summary>
    public static HealthRecord Standing(Branch branch)
    {
        ArgumentNullException.ThrowIfNull(branch);

        return new HealthRecord(
            branch.Label, branch.Worst.State, branch.Worst.Severity, string.Empty, default);
    }
}
