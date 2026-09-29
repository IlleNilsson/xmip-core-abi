using Xmip.Abi.Operate;

namespace Xmip.Surface;

/// <summary>
/// What a surface asks of the Event subscriptions it lists (ADR-0065,
/// amendment 2026-09-29), in the words every surface uses: the Subscriptions
/// view's address, <c>xmip-cli subscriptions</c>'s arguments and
/// <c>Get-XmipSubscription</c>'s parameters are these names. Where the drill
/// stands — a cluster, a node, one subscription — the scope pattern over
/// each subscription's node and reach, and the order: written once, here,
/// for every surface.
/// </summary>
public sealed record SubscriptionQuery
{
    /// <summary>The columns a list is ordered by, in the order a reader shows
    /// them. The first is the default.</summary>
    public static IReadOnlyList<string> Columns { get; } =
    [
        "subscriber", "cluster", "node", "action", "state", "queued", "delivered", "missed",
        "since",
    ];

    /// <summary>The scope pattern, <c>*</c> and <c>?</c>: a subscription
    /// matches when its node or the scope its filter reaches does.</summary>
    public string? Pattern { get; init; }

    /// <summary>Where the drill stands: a cluster or a node, and every
    /// subscription held at or beneath it.</summary>
    public string? Location { get; init; }

    /// <summary>One subscription's number on the node at
    /// <see cref="Location"/>.</summary>
    public ulong? Id { get; init; }

    /// <summary>A column of <see cref="Columns"/>; null is the first.</summary>
    public string? Sort { get; init; }

    /// <summary>ascending or descending; null is ascending.</summary>
    public string? Order { get; init; }

    /// <summary>Whether the order is greatest first.</summary>
    public bool Descending => string.Equals(Order, "descending", StringComparison.Ordinal);

    /// <summary>
    /// Who a subscription's subscriber is, as every surface shows it: the
    /// name its Party was declared with, and its identifier only where it was
    /// declared with none — shown as it is, never a name made from it.
    /// </summary>
    public static string Who(SubscriptionRecord subscription)
    {
        ArgumentNullException.ThrowIfNull(subscription);

        return subscription.Subscriber.Length > 0 ? subscription.Subscriber : subscription.Party;
    }

    /// <summary>The cluster a subscription is in: its node's first
    /// segment.</summary>
    public static string Cluster(SubscriptionRecord subscription)
    {
        ArgumentNullException.ThrowIfNull(subscription);

        return ScopeTree.Parts(subscription.Node) is [var cluster, ..] ? cluster : string.Empty;
    }

    /// <summary>The node's name, <c>R1</c> of <c>xmip:///C9/node/R1</c>.</summary>
    public static string NodeName(SubscriptionRecord subscription)
    {
        ArgumentNullException.ThrowIfNull(subscription);

        return ScopeTree.Node(subscription.Node);
    }

    /// <summary>
    /// What this query selects of <paramref name="subscriptions"/>, in its
    /// order, with ties by node and number.
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
                .ThenBy(entry => entry.Id),
        ];
    }

    private bool Selected(SubscriptionRecord subscription)
    {
        bool placed = Location is not { Length: > 0 } location
            || ScopeTree.Beneath(subscription.Node, location);
        bool numbered = Id is not { } id || subscription.Id == id;

        return placed && numbered
            && (Pattern is not { Length: > 0 } pattern
                || ScopePattern.Matches(subscription.Node, pattern)
                || ScopePattern.Matches(subscription.Scope, pattern));
    }

    private static IComparable Key(SubscriptionRecord entry, string sort)
    {
        return sort switch
        {
            "cluster" => Cluster(entry),
            "node" => NodeName(entry),
            "action" => entry.Action,
            "state" => entry.State,
            "queued" => entry.Queued,
            "delivered" => entry.Delivered,
            "missed" => entry.Missed,
            "since" => entry.Since,
            _ => Who(entry),
        };
    }
}
