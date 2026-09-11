using Xmip.Abi.Operate;

namespace Xmip.Surface;

/// <summary>
/// The scope tree, once. A scope is an Xmip URI (ADR-0027 clause 3) and the
/// tree beneath the cluster is <c>node / stage / name</c>; what is beneath
/// what, how a parent rolls up, and which leaf beneath a scope is worst are
/// decided here for every surface, so the board and the cmdlet cannot answer
/// differently (ADR-0052 clause 1).
/// </summary>
/// <remarks>
/// ADR-0041: a leaf's mood does not propagate. A parent is <c>Fine</c> when
/// every leaf beneath it is, and <c>Holding</c> the moment one is not; the
/// worst leaf carries the real mood and its evidence, and that is what a
/// Holding scope shows beside the word (ADR-0052 clause 2).
/// </remarks>
public static class ScopeTree
{
    /// <summary>The cluster: the root every scope is beneath.</summary>
    public const string Root = "xmip:///";

    private const string Scheme = "xmip://";

    /// <summary>
    /// The path segments of a scope: <c>xmip:///edge-01/receive/orders</c> is
    /// <c>edge-01</c>, <c>receive</c>, <c>orders</c>. The scheme and the
    /// authority go; an omitted host means estate-wide and the tree is the
    /// path. The root has no segments, so everything is beneath it.
    /// </summary>
    public static string[] Parts(string scope)
    {
        string path = scope;

        if (path.StartsWith(Scheme, StringComparison.Ordinal))
        {
            path = path[Scheme.Length..];
            int slash = path.IndexOf('/', StringComparison.Ordinal);
            path = slash < 0 ? string.Empty : path[(slash + 1)..];
        }

        return path.Split('/', StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>A scope built from segments, under the root.</summary>
    public static string Join(IEnumerable<string> parts)
    {
        return Root + string.Join('/', parts);
    }

    /// <summary>Whether <paramref name="candidate"/> is <paramref name="scope"/>
    /// itself or beneath it: the scope's segments are a prefix of the
    /// candidate's.</summary>
    public static bool Beneath(string candidate, string scope)
    {
        string[] want = Parts(scope);
        string[] have = Parts(candidate);

        if (have.Length < want.Length)
        {
            return false;
        }

        for (int index = 0; index < want.Length; index++)
        {
            if (!string.Equals(have[index], want[index], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
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

    /// <summary>One segment by position, or empty when the scope is not that
    /// deep. Position 1 is the stage: <c>receive</c>, <c>process</c> or
    /// <c>send</c>.</summary>
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
    /// Holding, and only Fine when everything beneath is.</summary>
    public static HealthState Rolled(HealthState worst)
    {
        return worst == HealthState.Fine ? HealthState.Fine : HealthState.Holding;
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
    /// health in.</summary>
    public static IReadOnlyList<HealthRecord> WorstFirst(IEnumerable<HealthRecord> records)
    {
        return
        [
            .. records
                .OrderByDescending(record => record.State)
                .ThenByDescending(record => record.Severity)
                .ThenBy(record => record.Scope, StringComparer.Ordinal),
        ];
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

        return
        [
            .. branches
                .OrderByDescending(branch => branch.Worst.State)
                .ThenByDescending(branch => branch.Worst.Severity)
                .ThenBy(branch => branch.Label, StringComparer.Ordinal),
        ];
    }
}
