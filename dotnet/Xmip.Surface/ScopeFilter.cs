using System.Globalization;
using Xmip.Abi.Operate;

namespace Xmip.Surface;

/// <summary>
/// One <see cref="ScopePattern"/> applied to one publication: which scopes it
/// names, what stands on the way to them, and the one sentence a surface says
/// about it. A view narrows what it shows through this, so the three views
/// narrow alike (ADR-0052 clause 1) and none of them decides for itself what a
/// pattern means.
/// </summary>
/// <remarks>
/// What a filtered view shows is the match, the path down to it, and what lies
/// beneath it: a pattern that names a node shows that node's subtree, and a
/// pattern that names a leaf still shows the branches an operator must open to
/// see it. Nothing matched is never an empty page: <see cref="Said"/> is the
/// refusal, in the estate's words, and the surface puts it where the eye is.
/// </remarks>
public sealed class ScopeFilter
{
    /// <summary>No pattern: everything is shown, and nothing is said.</summary>
    public static readonly ScopeFilter None = new(string.Empty, [], 0);

    private readonly HashSet<string> matched;

    private readonly HashSet<string> onTheWay;

    private ScopeFilter(string pattern, IReadOnlyList<string> matched, int published)
    {
        Pattern = pattern;
        Published = published;
        this.matched = new HashSet<string>(matched, StringComparer.Ordinal);
        onTheWay = new HashSet<string>(StringComparer.Ordinal);

        foreach (string scope in matched)
        {
            for (string above = scope;
                above != ScopeTree.Root;
                above = ScopeTree.Parent(above))
            {
                onTheWay.Add(above);
            }
        }

        onTheWay.Add(ScopeTree.Root);
    }

    /// <summary>The pattern as the operator typed it; empty when there is none.</summary>
    public string Pattern { get; }

    /// <summary>Whether anything is being narrowed at all.</summary>
    public bool Filtering => Pattern.Length > 0;

    /// <summary>How many scopes the pattern named.</summary>
    public int Matched => matched.Count;

    /// <summary>How many scopes the publication holds, branches included.</summary>
    public int Published { get; }

    /// <summary>What a surface says about the filter beside the box: nothing
    /// when there is no pattern, the count when it named something, and the
    /// refusal when it named nothing — an empty page must never read as a
    /// healthy estate.</summary>
    public string Said => !Filtering
        ? string.Empty
        : Matched == 0
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"REFUSED — nothing matches {Pattern}. {Published:N0} scope(s) published.")
            : string.Create(
                CultureInfo.InvariantCulture,
                $"{Matched:N0} of {Published:N0} scope(s) match {Pattern}.");

    /// <summary>The pattern over one publication's scopes.</summary>
    public static ScopeFilter Over(ScopeIndex index, string? pattern)
    {
        string typed = pattern?.Trim() ?? string.Empty;

        return typed.Length == 0
            ? None
            : new ScopeFilter(typed, index.Matching(typed), index.Scopes);
    }

    /// <summary>Whether the pattern itself named this scope.</summary>
    public bool IsMatch(string scope)
    {
        return matched.Contains(ScopePattern.Normal(scope));
    }

    /// <summary>Whether a view shows this scope: it matched, it stands on the
    /// way down to something that matched, or it lies beneath something that
    /// did. What a tree asks — a row that cannot be reached is a row that is
    /// not there.</summary>
    public bool Shows(string scope)
    {
        return !Filtering
            || onTheWay.Contains(ScopePattern.Normal(scope))
            || Holds(scope);
    }

    /// <summary>
    /// Whether a scope is inside what the pattern named: it matched, or it
    /// lies beneath something that did. What a flat list of leaves asks, where
    /// nothing has to stand open for a row to be reached, and the branch above
    /// a match is not itself part of the answer.
    /// </summary>
    public bool Holds(string scope)
    {
        if (!Filtering)
        {
            return true;
        }

        for (string above = ScopePattern.Normal(scope);
            above != ScopeTree.Root;
            above = ScopeTree.Parent(above))
        {
            if (matched.Contains(above))
            {
                return true;
            }
        }

        return matched.Contains(ScopeTree.Root);
    }

    /// <summary>The branches a view still shows.</summary>
    public IReadOnlyList<Branch> Only(IEnumerable<Branch> branches)
    {
        return Filtering ? [.. branches.Where(branch => Shows(branch.Scope))] : [.. branches];
    }

    /// <summary>The leaves a view still shows. A list of leaves is flat, so it
    /// asks <see cref="Holds"/>: the branch above a match is a row of the tree,
    /// not a leaf the pattern named.</summary>
    public IReadOnlyList<HealthRecord> Only(IEnumerable<HealthRecord> records)
    {
        return Filtering ? [.. records.Where(record => Holds(record.Scope))] : [.. records];
    }

    /// <summary>The topology a view still draws: the nodes that are shown, and
    /// a link only where both of its ends are — a line to something hidden
    /// points at nothing.</summary>
    public TopologySnapshot Only(TopologySnapshot snapshot)
    {
        if (!Filtering)
        {
            return snapshot;
        }

        IReadOnlyList<TopologyNode> nodes = [.. snapshot.Nodes.Where(node => Shows(node.Scope))];
        HashSet<string> drawn = new(nodes.Select(node => node.Id), StringComparer.Ordinal);

        return snapshot with
        {
            Nodes = nodes,
            Links =
            [
                .. snapshot.Links.Where(link =>
                    drawn.Contains(link.From) && drawn.Contains(link.To)),
            ],
        };
    }
}
