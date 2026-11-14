namespace Xmip.Abi.Operate;

/// <summary>
/// Which publication a remote node's follow follows (section 17): the node's
/// own, the cluster's view a node declaring the monitoring role gathers, or
/// — <see cref="Answered"/> — the cluster's where the node answers one, else
/// its own, the node's answer deciding as each connection to it is made. A
/// handle follows the one its first wait names; naming another later is
/// refused. The values are the header's <c>XMIP_REMOTE_FOLLOWS_*</c>.
/// </summary>
public enum RemoteFollows
{
    /// <summary>The node's own publication.</summary>
    Node = 0,

    /// <summary>The cluster's view the node gathers.</summary>
    Cluster = 1,

    /// <summary>The cluster's view where the node answers one, else its own.
    /// What a follow says it follows while the node's answer has not decided,
    /// and while the node is lost.</summary>
    Answered = 2,
}
