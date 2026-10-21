namespace Xmip.Abi.Operate;

// Section 21's symbols: a web host's HTTPS in Xmip's own TLS (ADR-0009,
// amendment 2026-10-21).
public static partial class OperateAbi
{
    /// <summary>A web host's certificate under an authority kept in a folder:
    /// section 21 (ADR-0009, amendment 2026-10-21).</summary>
    public const string HostIssueEntrypoint = "xmip_host_issue_v1";

    /// <summary>A web host's HTTPS listener in Xmip's own TLS: section 21.</summary>
    public const string ListenerOpenEntrypoint = "xmip_listener_open_v1";

    /// <summary>Where an HTTPS listener listens: section 21.</summary>
    public const string ListenerAddressEntrypoint = "xmip_listener_address_v1";

    /// <summary>The next connection whose handshake finished: section 21.</summary>
    public const string ListenerAcceptEntrypoint = "xmip_listener_accept_v1";

    /// <summary>An HTTPS listener stopped, every accept woken: section 21.</summary>
    public const string ListenerStopEntrypoint = "xmip_listener_stop_v1";

    /// <summary>An HTTPS listener freed: section 21.</summary>
    public const string ListenerFreeEntrypoint = "xmip_listener_free_v1";

    /// <summary>The certificate a session's browser presented: section 21.</summary>
    public const string SessionPeerEntrypoint = "xmip_session_peer_v1";

    /// <summary>Where a session was accepted and where its browser is:
    /// section 21.</summary>
    public const string SessionEndsEntrypoint = "xmip_session_ends_v1";

    /// <summary>What a session's browser sent next: section 21.</summary>
    public const string SessionReadEntrypoint = "xmip_session_read_v1";

    /// <summary>Bytes written to a session's browser: section 21.</summary>
    public const string SessionWriteEntrypoint = "xmip_session_write_v1";

    /// <summary>A session closed, a read waiting woken: section 21.</summary>
    public const string SessionCloseEntrypoint = "xmip_session_close_v1";

    /// <summary>A session freed: section 21.</summary>
    public const string SessionFreeEntrypoint = "xmip_session_free_v1";
}
