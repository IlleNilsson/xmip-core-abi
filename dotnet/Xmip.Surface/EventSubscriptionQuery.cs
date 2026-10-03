using Xmip.Abi.Operate;

namespace Xmip.Surface;

/// <summary>
/// What a surface asks of the Event subscriptions it lists (ADR-0065,
/// amendment 2026-09-29), in the words every surface uses: the Event
/// subscriptions view's address, <c>xmip-cli event-subscriptions</c>'s
/// arguments and <c>Get-XmipEventSubscription</c>'s parameters are these
/// names. Where the drill stands — a cluster, a node, one Event
/// subscription — the scope pattern over
/// each subscription's node and reach, and the order: written once, here,
/// for every surface.
/// </summary>
public sealed record EventSubscriptionQuery
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
    public static string Who(EventSubscriptionRecord subscription)
    {
        ArgumentNullException.ThrowIfNull(subscription);

        return subscription.Subscriber.Length > 0 ? subscription.Subscriber : subscription.Party;
    }

    /// <summary>The cluster a subscription is in: its node's first
    /// segment.</summary>
    public static string Cluster(EventSubscriptionRecord subscription)
    {
        ArgumentNullException.ThrowIfNull(subscription);

        return ScopeTree.Parts(subscription.Node) is [var cluster, ..] ? cluster : string.Empty;
    }

    /// <summary>The node's name, the segment after <c>node/</c> of
    /// <c>xmip:///&lt;cluster&gt;/node/&lt;node&gt;</c>.</summary>
    public static string NodeName(EventSubscriptionRecord subscription)
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
    public IReadOnlyList<EventSubscriptionRecord> Apply(IEnumerable<EventSubscriptionRecord> subscriptions)
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

        IEnumerable<EventSubscriptionRecord> chosen = subscriptions.Where(Selected);
        IOrderedEnumerable<EventSubscriptionRecord> ordered = Descending
            ? chosen.OrderByDescending(entry => Key(entry, sort), Comparer<IComparable>.Default)
            : chosen.OrderBy(entry => Key(entry, sort), Comparer<IComparable>.Default);

        return
        [
            .. ordered
                .ThenBy(entry => entry.Node, StringComparer.Ordinal)
                .ThenBy(entry => entry.Id),
        ];
    }

    /// <summary>
    /// The read-only line every surface shows for a member not heard: the
    /// node that does not hear it, then the runtime's own words —
    /// <c>&lt;node&gt;: not hearing xmip:///&lt;cluster&gt;/node/&lt;member&gt; since
    /// 2026-10-02T12:00:00Z: connection refused</c>.
    /// </summary>
    public static string Line(UnheardRecord unheard)
    {
        ArgumentNullException.ThrowIfNull(unheard);

        return $"{ScopeTree.Node(unheard.By)}: {unheard.Said}";
    }

    /// <summary>
    /// The members not heard by the nodes this query stands at — each said in
    /// its one line, <c>not hearing &lt;node&gt; since &lt;time&gt;: &lt;why&gt;</c>
    /// — by node, then member: what every surface shows read-only beside the
    /// Event subscriptions, so no Event is missing silently (ADR-0065,
    /// amendment 2026-10-02). The links themselves are never listed.
    /// </summary>
    public IReadOnlyList<UnheardRecord> Unheard(IEnumerable<UnheardRecord> unheard)
    {
        ArgumentNullException.ThrowIfNull(unheard);

        return
        [
            .. unheard
                .Where(gone => Location is not { Length: > 0 } location
                    || ScopeTree.Beneath(gone.By, location)
                    || ScopeTree.Beneath(location, gone.By))
                .OrderBy(gone => gone.By, StringComparer.Ordinal)
                .ThenBy(gone => gone.Node, StringComparer.Ordinal),
        ];
    }

    private bool Selected(EventSubscriptionRecord subscription)
    {
        bool placed = Location is not { Length: > 0 } location
            || ScopeTree.Beneath(subscription.Node, location);
        bool numbered = Id is not { } id || subscription.Id == id;

        return placed && numbered
            && (Pattern is not { Length: > 0 } pattern
                || ScopePattern.Matches(subscription.Node, pattern)
                || ScopePattern.Matches(subscription.Scope, pattern));
    }

    private static IComparable Key(EventSubscriptionRecord entry, string sort)
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
