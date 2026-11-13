namespace Xmip.Abi.Operate;

/// <summary>The kind of thing shown in the communication topology. The values
/// are the header's <c>XMIP_TOPOLOGY_*</c> (section 8); the words a
/// publication writes are <c>observe::topology</c>'s, and no surface reads
/// them (open problem 25).</summary>
public enum TopologyNodeKind
{
    /// <summary>A physical or logical computer.</summary>
    Computer = 0,
    /// <summary>A server.</summary>
    Server = 1,
    /// <summary>A virtual machine.</summary>
    VirtualMachine = 2,
    /// <summary>A network or application gateway.</summary>
    Gateway = 3,
    /// <summary>An infrastructure appliance.</summary>
    Appliance = 4,
    /// <summary>A service hosted by an infrastructure endpoint.</summary>
    Service = 5,
    /// <summary>An operating-system process.</summary>
    Process = 6,
    /// <summary>A network interface.</summary>
    Interface = 7,
    /// <summary>A local or remote port.</summary>
    Port = 8,
    /// <summary>A transport or application protocol endpoint.</summary>
    Protocol = 9,
    /// <summary>A configured logical or physical location.</summary>
    Location = 10,
    /// <summary>An Xmip cluster: the root its nodes hang under.</summary>
    Cluster = 11,
    /// <summary>One Xmip node of a cluster.</summary>
    Node = 12,
    /// <summary>A stage of the message path on a node: receive, process or send.</summary>
    Stage = 13,
    /// <summary>A receiving or sending endpoint of a stage, one per transport.</summary>
    Endpoint = 14,
    /// <summary>A Party outside the cluster (ADR-0019): one that sends into a Receive
    /// Location, or one a Send Location delivers to; its links say which.</summary>
    Party = 15,
}

/// <summary>Where a topology fact came from: the header's
/// <c>XMIP_ORIGIN_*</c>.</summary>
public enum TopologyOrigin
{
    /// <summary>Declared by configuration but not observed in the current window.</summary>
    Configured = 0,
    /// <summary>Observed at runtime but not present in the loaded configuration.</summary>
    Observed = 1,
    /// <summary>Both configured and observed.</summary>
    Both = 2,
}

/// <summary>The application meaning of communication, separate from wire traffic:
/// the header's <c>XMIP_PATTERN_*</c>.</summary>
public enum CommunicationPattern
{
    /// <summary>A request followed by its correlated response.</summary>
    RequestResponse = 0,
    /// <summary>One payload sent to a distinct receive or completion event.</summary>
    SendReceive = 1,
    /// <summary>A producer publishes to an intermediary and a consumer later takes it.</summary>
    PublishConsume = 2,
    /// <summary>Sustained producer-to-consumer data flow.</summary>
    Streaming = 3,
    /// <summary>A one-way application send with no expected application reply.</summary>
    FireAndForget = 4,
    /// <summary>Independent application messages in either direction on a
    /// persistent connection.</summary>
    Session = 5,
    /// <summary>An unsuccessful delivery followed by one or more attempts.</summary>
    Retry = 6,
}

/// <summary>How the two ends of a link are related in their cluster, by the
/// roles each declares and the order of <c>[storage] nodes</c>, never by a
/// name: the header's <c>XMIP_RELATION_*</c> (<c>observe::Relation</c>;
/// ADR-0052 and ADR-0056, amendments 2026-11-13).</summary>
public enum TopologyRelation
{
    /// <summary>A node that does not declare storage, into the Storage node it
    /// writes into.</summary>
    WritesInto = 0,
    /// <summary>That Storage node, to each node whose roles serve process or
    /// send.</summary>
    HandsWorkTo = 1,
    /// <summary>The first-listed Storage node, to each later-listed one.</summary>
    CopiesTo = 2,
    /// <summary>A node declaring the operational role, to every other member.</summary>
    Operates = 3,
    /// <summary>A node declaring the monitoring role, to every other member.</summary>
    Monitors = 4,
    /// <summary>A node declaring both, to every other member.</summary>
    OperatesAndMonitors = 5,
    /// <summary>An observed line no relation explains: inside a node, and
    /// between a node and a Party.</summary>
    Traffic = 6,
}

/// <summary>A horizontal band of the map, top to bottom: the header's
/// <c>XMIP_BAND_*</c> (<c>observe::topology::layout::Band</c>).</summary>
public enum TopologyBand
{
    /// <summary>Operational, monitoring, development, none declared: on top.</summary>
    Operation = 0,
    /// <summary>Processing alone, over the Storage column.</summary>
    Processing = 1,
    /// <summary>The path: receiving, the first-listed Storage node, sending,
    /// and the Parties beside them.</summary>
    Path = 2,
    /// <summary>The later-listed Storage nodes, below the first.</summary>
    Standby = 3,
    /// <summary>Executing, or two stage roles: a path of its own below.</summary>
    Executing = 4,
    /// <summary>Inside a node or a stage: what a drill shows.</summary>
    Inside = 5,
}

/// <summary>A column of the map, left to right as the streams run: the
/// header's <c>XMIP_COLUMN_*</c> (<c>observe::topology::layout::Column</c>).</summary>
public enum TopologyColumn
{
    /// <summary>The Parties that send into the cluster, facing the nodes that
    /// receive: the header's <c>XMIP_COLUMN_TX_PARTY</c>.</summary>
    TxParty = 0,
    /// <summary>Receiving, and the Receive stage inside a node.</summary>
    Receiving = 1,
    /// <summary>Storage, and the Work Process stage inside a node.</summary>
    Storage = 2,
    /// <summary>Sending, and the Send stage inside a node.</summary>
    Sending = 3,
    /// <summary>The Parties the cluster delivers to, facing the nodes that
    /// send: the header's <c>XMIP_COLUMN_RX_PARTY</c>.</summary>
    RxParty = 4,
    /// <summary>No one column: the operation band, the executing lane, a grid.</summary>
    Spanning = 5,
}

/// <summary>
/// One infrastructure endpoint or a component beneath it. <paramref name="ParentId"/>
/// forms the progressive drill-down; a null parent is an infrastructure-level node.
/// Load is normalized from zero to one; <paramref name="Activity"/> is what moved
/// through it per second over its publisher's window
/// (<c>observe::topology::window</c>), zero where nothing moved. A node of the
/// cluster says the <paramref name="Roles"/> it declares, comma separated, empty
/// for none and for anything that is no node; a Storage node listed in
/// <c>[storage] nodes</c> its <paramref name="Place"/> there, from zero, null for
/// anything else (ADR-0052 and ADR-0056, amendments 2026-11-13).
/// </summary>
public sealed record TopologyNode(
    string Id,
    string? ParentId,
    string Label,
    TopologyNodeKind Kind,
    string Scope,
    HealthState State,
    TopologyOrigin Origin,
    double Load,
    double Activity,
    string Evidence,
    string Roles = "",
    int? Place = null);

/// <summary>
/// One semantic communication relationship. Transport acknowledgements are not
/// represented as a reverse application flow; <paramref name="Pattern"/> says
/// what the application exchange means, and <paramref name="Relation"/> how its
/// two ends are related in their cluster.
/// </summary>
public sealed record CommunicationLink(
    string Id,
    string From,
    string To,
    CommunicationPattern Pattern,
    TopologyOrigin Origin,
    string Protocol,
    HealthState State,
    ulong Volume,
    double Rate,
    double LatencyMilliseconds,
    double Progress,
    uint Attempts,
    string Evidence,
    TopologyRelation Relation = TopologyRelation.Traffic);

/// <summary>Where one drawn thing stands on the map, as the runtime lays it
/// out (<c>observe::topology::layout</c>, <c>xmip_publication_layout_v1</c>):
/// <paramref name="X"/> and <paramref name="Y"/> in the unit square, left to
/// right and top to bottom, which a surface scales to its canvas, and the
/// band and column it stands in.</summary>
public sealed record TopologyPlace(
    string Id,
    double X,
    double Y,
    TopologyBand Band,
    TopologyColumn Column);

/// <summary>A point-in-time configured and observed communication topology,
/// where the work at each thing it draws came from and went, by the scope it
/// is drawn at, as the runtime says it (<c>observe::topology::from_to</c>,
/// section 8; ADR-0027, amendment 2026-11-11), and where each thing beneath
/// each focus stands, by the focus's id, the empty string for the top
/// (<c>observe::topology::layout</c>; ADR-0052, amendment 2026-11-13).</summary>
public sealed record TopologySnapshot(
    IReadOnlyList<TopologyNode> Nodes,
    IReadOnlyList<CommunicationLink> Links,
    DateTimeOffset Observed,
    string Source,
    IReadOnlyDictionary<string, FromTo>? Paths = null,
    IReadOnlyDictionary<string, IReadOnlyList<TopologyPlace>>? Places = null)
{
    /// <summary>An empty snapshot from a surface that publishes no topology.</summary>
    public static TopologySnapshot Empty(string source)
    {
        return new TopologySnapshot([], [], DateTimeOffset.UtcNow, source);
    }

    /// <summary>Where each thing beneath <paramref name="focus"/> stands, as the
    /// runtime laid it out — null or empty for the top; an empty list where
    /// nothing was laid out there.</summary>
    public IReadOnlyList<TopologyPlace> PlacesAt(string? focus)
    {
        return Places is not null
            && Places.TryGetValue(focus ?? string.Empty, out IReadOnlyList<TopologyPlace>? placed)
            ? placed
            : [];
    }
}
