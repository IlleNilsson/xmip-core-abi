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
    /// <summary>Independent application messages in either direction on a persistent connection.</summary>
    Session = 5,
    /// <summary>An unsuccessful delivery followed by one or more attempts.</summary>
    Retry = 6,
}

/// <summary>
/// One infrastructure endpoint or a component beneath it. <paramref name="ParentId"/>
/// forms the progressive drill-down; a null parent is an infrastructure-level node.
/// Load and activity are normalized from zero to one.
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
    string Evidence);

/// <summary>
/// One semantic communication relationship. Transport acknowledgements are not
/// represented as a reverse application flow; <paramref name="Pattern"/> says
/// what the application exchange means.
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
    string Evidence);

/// <summary>A point-in-time configured and observed communication topology.</summary>
public sealed record TopologySnapshot(
    IReadOnlyList<TopologyNode> Nodes,
    IReadOnlyList<CommunicationLink> Links,
    DateTimeOffset Observed,
    string Source)
{
    /// <summary>An empty snapshot from a surface that publishes no topology.</summary>
    public static TopologySnapshot Empty(string source)
    {
        return new TopologySnapshot([], [], DateTimeOffset.UtcNow, source);
    }
}
