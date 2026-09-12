using Xmip.Abi.Operate;

namespace Xmip.Surface;

/// <summary>The kind of thing shown in the communication topology.</summary>
public enum TopologyNodeKind
{
    /// <summary>A physical or logical computer.</summary>
    Computer,
    /// <summary>A server.</summary>
    Server,
    /// <summary>A virtual machine.</summary>
    VirtualMachine,
    /// <summary>A network or application gateway.</summary>
    Gateway,
    /// <summary>An infrastructure appliance.</summary>
    Appliance,
    /// <summary>A service hosted by an infrastructure endpoint.</summary>
    Service,
    /// <summary>An operating-system process.</summary>
    Process,
    /// <summary>A network interface.</summary>
    Interface,
    /// <summary>A local or remote port.</summary>
    Port,
    /// <summary>A transport or application protocol endpoint.</summary>
    Protocol,
    /// <summary>A configured logical or physical location.</summary>
    Location,
}

/// <summary>Where a topology fact came from.</summary>
public enum TopologyOrigin
{
    /// <summary>Declared by configuration but not observed in the current window.</summary>
    Configured,
    /// <summary>Observed at runtime but not present in the loaded configuration.</summary>
    Observed,
    /// <summary>Both configured and observed.</summary>
    Both,
}

/// <summary>The application meaning of communication, separate from wire traffic.</summary>
public enum CommunicationPattern
{
    /// <summary>A request followed by its correlated response.</summary>
    RequestResponse,
    /// <summary>One payload sent to a distinct receive or completion event.</summary>
    SendReceive,
    /// <summary>A producer publishes to an intermediary and a consumer later takes it.</summary>
    PublishConsume,
    /// <summary>Sustained producer-to-consumer data flow.</summary>
    Streaming,
    /// <summary>A one-way application send with no expected application reply.</summary>
    FireAndForget,
    /// <summary>Independent application messages in either direction on a persistent connection.</summary>
    Session,
    /// <summary>An unsuccessful delivery followed by one or more attempts.</summary>
    Retry,
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
