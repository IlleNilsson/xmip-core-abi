using Xmip.Abi.Operate;

namespace Xmip.Surface;

/// <summary>
/// What a surface asks of the Subscriptions it lists (ADR-0013, amendment
/// 2026-09-30), in the words every surface uses: the Subscriptions view's
/// address, <c>xmip-cli subscriptions</c>'s arguments and
/// <c>Get-XmipSubscription</c>'s parameters are these names. Where the drill
/// stands — a cluster, a node, one Subscription — the scope pattern over
/// each Subscription's node and name, and the order: written once, here, for
/// every surface.
/// </summary>
public sealed record SubscriptionQuery
{
    /// <summary>The columns a list is ordered by, in the order a reader shows
    /// them. The first is the default.</summary>
    public static IReadOnlyList<string> Columns { get; } =
    [
        "subscription", "cluster", "node", "filter", "destination", "state", "picked-up",
        "held", "since",
    ];

    /// <summary>The scope pattern, <c>*</c> and <c>?</c>: a Subscription
    /// matches when its node, or its node and name as one scope, does.</summary>
    public string? Pattern { get; init; }

    /// <summary>Where the drill stands: a cluster or a node, and every
    /// Subscription routed by at or beneath it.</summary>
    public string? Location { get; init; }

    /// <summary>One Subscription's name on the node at
    /// <see cref="Location"/>.</summary>
    public string? Name { get; init; }

    /// <summary>A column of <see cref="Columns"/>; null is the first.</summary>
    public string? Sort { get; init; }

    /// <summary>ascending or descending; null is ascending.</summary>
    public string? Order { get; init; }

    /// <summary>Whether the order is greatest first.</summary>
    public bool Descending => string.Equals(Order, "descending", StringComparison.Ordinal);

    /// <summary>The cluster a Subscription is in: its node's first
    /// segment.</summary>
    public static string Cluster(SubscriptionRecord subscription)
    {
        ArgumentNullException.ThrowIfNull(subscription);

        return ScopeTree.Parts(subscription.Node) is [var cluster, ..] ? cluster : string.Empty;
    }

    /// <summary>The node's name, <c>beta</c> of <c>xmip:///CT/node/beta</c>.</summary>
    public static string NodeName(SubscriptionRecord subscription)
    {
        ArgumentNullException.ThrowIfNull(subscription);

        return ScopeTree.Node(subscription.Node);
    }

    /// <summary>A Subscription as one scope: its node's, then its name —
    /// what a pattern is matched against beside the node.</summary>
    public static string Scope(SubscriptionRecord subscription)
    {
        ArgumentNullException.ThrowIfNull(subscription);

        return $"{subscription.Node}/subscription/{subscription.Name}";
    }

    /// <summary>
    /// What this query selects of <paramref name="subscriptions"/>, in its
    /// order, with ties by node and name.
    /// </summary>
    /// <exception cref="ArgumentException">REFUSED: a sort that is no column or
    /// an order that is neither word.</exception>
    public IReadOnlyList<SubscriptionRecord> Apply(IEnumerable<SubscriptionRecord> subscriptions)
    {
        ArgumentNullException.ThrowIfNull(subscriptions);

        string sort = Sort ?? Columns[0];
        if (!Columns.Contains(sort))
        {
            throw new ArgumentException(
                $"REFUSED: '{sort}' is no column; the columns are {string.Join(", ", Columns)}.");
        }

        if (Order is not (null or "ascending" or "descending"))
        {
            throw new ArgumentException(
                $"REFUSED: '{Order}' is no order; the orders are ascending, descending.");
        }

        IEnumerable<SubscriptionRecord> chosen = subscriptions.Where(Selected);
        IOrderedEnumerable<SubscriptionRecord> ordered = Descending
            ? chosen.OrderByDescending(entry => Key(entry, sort), Comparer<IComparable>.Default)
            : chosen.OrderBy(entry => Key(entry, sort), Comparer<IComparable>.Default);

        return
        [
            .. ordered
                .ThenBy(entry => entry.Node, StringComparer.Ordinal)
                .ThenBy(entry => entry.Name, StringComparer.Ordinal),
        ];
    }

    private bool Selected(SubscriptionRecord subscription)
    {
        bool placed = Location is not { Length: > 0 } location
            || ScopeTree.Beneath(subscription.Node, location);
        bool named = Name is not { Length: > 0 } name
            || string.Equals(subscription.Name, name, StringComparison.Ordinal);

        return placed && named
            && (Pattern is not { Length: > 0 } pattern
                || ScopePattern.Matches(subscription.Node, pattern)
                || ScopePattern.Matches(Scope(subscription), pattern));
    }

    private static IComparable Key(SubscriptionRecord entry, string sort)
    {
        return sort switch
        {
            "cluster" => Cluster(entry),
            "node" => NodeName(entry),
            "filter" => entry.Filter,
            "destination" => entry.Destination,
            "state" => entry.State,
            "picked-up" => entry.PickedUp,
            "held" => entry.Held,
            "since" => entry.Since,
            _ => entry.Name,
        };
    }
}
