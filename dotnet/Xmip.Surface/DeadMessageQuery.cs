using Xmip.Abi.Operate;

namespace Xmip.Surface;

/// <summary>
/// What a surface asks of the Dead Message Queues it lists (ADR-0052,
/// amendment 2026-10-01), in the words every surface uses: the Dead Message
/// Queue view's address, <c>xmip-cli dead-messages</c>'s arguments and
/// <c>Get-XmipDeadMessage</c>'s parameters are these names. Where the drill
/// stands — a cluster, a node, one Message — the scope pattern over each
/// entry's node and Message, and the order: written once, here, for every
/// surface.
/// </summary>
public sealed record DeadMessageQuery
{
    /// <summary>The columns a list is ordered by, in the order a reader shows
    /// them. The first is the default: the oldest first.</summary>
    public static IReadOnlyList<string> Columns { get; } =
    [
        "received", "message", "cluster", "node", "receive-location", "declines",
    ];

    /// <summary>The scope pattern, <c>*</c> and <c>?</c>: an entry matches
    /// when its node, or its node and Message as one scope, does.</summary>
    public string? Pattern { get; init; }

    /// <summary>Where the drill stands: a cluster or a node, and every entry
    /// kept by a queue at or beneath it.</summary>
    public string? Location { get; init; }

    /// <summary>One Message, by its identifier, in the queue of the node at
    /// <see cref="Location"/>.</summary>
    public string? Message { get; init; }

    /// <summary>A column of <see cref="Columns"/>; null is the first.</summary>
    public string? Sort { get; init; }

    /// <summary>ascending or descending; null is ascending.</summary>
    public string? Order { get; init; }

    /// <summary>Whether the order is greatest first.</summary>
    public bool Descending => string.Equals(Order, "descending", StringComparison.Ordinal);

    /// <summary>The cluster an entry is in: its node's first segment.</summary>
    public static string Cluster(DeadMessageRecord entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return ScopeTree.Parts(entry.Node) is [var cluster, ..] ? cluster : string.Empty;
    }

    /// <summary>The node's name, the segment after <c>node/</c> of
    /// <c>xmip:///&lt;cluster&gt;/node/&lt;node&gt;</c>.</summary>
    public static string NodeName(DeadMessageRecord entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return ScopeTree.Node(entry.Node);
    }

    /// <summary>An entry as one scope: its node's, then its Message — what a
    /// pattern is matched against beside the node.</summary>
    public static string Scope(DeadMessageRecord entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return $"{entry.Node}/{DeadMessageOperation.Noun}/{entry.Message}";
    }

    /// <summary>
    /// What this query selects of <paramref name="entries"/>, in its order,
    /// with ties by node and place in the queue.
    /// </summary>
    /// <exception cref="ArgumentException">REFUSED: a sort that is no column or
    /// an order that is neither word.</exception>
    public IReadOnlyList<DeadMessageRecord> Apply(IEnumerable<DeadMessageRecord> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

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

        IEnumerable<DeadMessageRecord> chosen = entries.Where(Selected);
        IOrderedEnumerable<DeadMessageRecord> ordered = Descending
            ? chosen.OrderByDescending(entry => Key(entry, sort), Comparer<IComparable>.Default)
            : chosen.OrderBy(entry => Key(entry, sort), Comparer<IComparable>.Default);

        return
        [
            .. ordered
                .ThenBy(entry => entry.Node, StringComparer.Ordinal)
                .ThenBy(entry => entry.Sequence),
        ];
    }

    private bool Selected(DeadMessageRecord entry)
    {
        bool placed = Location is not { Length: > 0 } location
            || ScopeTree.Beneath(entry.Node, location);
        bool named = Message is not { Length: > 0 } message
            || string.Equals(entry.Message, message, StringComparison.Ordinal);

        return placed && named
            && (Pattern is not { Length: > 0 } pattern
                || ScopePattern.Matches(entry.Node, pattern)
                || ScopePattern.Matches(Scope(entry), pattern));
    }

    private static IComparable Key(DeadMessageRecord entry, string sort)
    {
        return sort switch
        {
            "message" => entry.Message,
            "cluster" => Cluster(entry),
            "node" => NodeName(entry),
            "receive-location" => entry.ReceiveLocation,
            "declines" => entry.Declines.Count,
            _ => entry.Received,
        };
    }
}
