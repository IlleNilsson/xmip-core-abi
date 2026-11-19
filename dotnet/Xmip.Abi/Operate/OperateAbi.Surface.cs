namespace Xmip.Abi.Operate;

// Section 28.2's symbols: a surface, opened, read, followed and asked to
// act (ADR-0052, amendment 2026-11-18).
public static partial class OperateAbi
{
    /// <summary>The surface over a published snapshot: section 28.2.</summary>
    public const string SurfaceSnapshotEntrypoint = "xmip_surface_snapshot_v1";

    /// <summary>The surface over a node at its local endpoint.</summary>
    public const string SurfaceLocalEntrypoint = "xmip_surface_local_v1";

    /// <summary>The surface over a node at its operate address.</summary>
    public const string SurfaceRemoteEntrypoint = "xmip_surface_remote_v1";

    /// <summary>The same, its key a keeper's.</summary>
    public const string SurfaceRemoteKeptEntrypoint = "xmip_surface_remote_kept_v1";

    /// <summary>A surface released, its follow ended.</summary>
    public const string SurfaceFreeEntrypoint = "xmip_surface_free_v1";

    /// <summary>Where a surface's answers come from.</summary>
    public const string SurfaceSourceEntrypoint = "xmip_surface_source_v1";

    /// <summary>How a surface stands, nothing asked of a node.</summary>
    public const string SurfaceStandingEntrypoint = "xmip_surface_standing_v1";

    /// <summary>Whether a surface answers now.</summary>
    public const string SurfaceAnswersEntrypoint = "xmip_surface_answers_v1";

    /// <summary>What a surface read, where it changed.</summary>
    public const string SurfaceReadEntrypoint = "xmip_surface_read_v1";

    /// <summary>A surface followed, told each change.</summary>
    public const string SurfaceWaitEntrypoint = "xmip_surface_wait_v1";

    /// <summary>An act on one of a node's nouns.</summary>
    public const string SurfaceActEntrypoint = "xmip_surface_act_v1";

    /// <summary>An act on a failed Message.</summary>
    public const string SurfaceActMessageEntrypoint = "xmip_surface_act_message_v1";

    /// <summary>A scope paused or resumed.</summary>
    public const string SurfaceControlEntrypoint = "xmip_surface_control_v1";

    /// <summary>The Messages that failed, Port by Port.</summary>
    public const string SurfaceFailedEntrypoint = "xmip_surface_failed_v1";

    /// <summary>The secrets a node keeps, never a value.</summary>
    public const string SurfaceSecretsEntrypoint = "xmip_surface_secrets_v1";

    /// <summary>A secret set through a node.</summary>
    public const string SurfaceSetSecretEntrypoint = "xmip_surface_set_secret_v1";

    /// <summary>The surface a line and a host's document choose.</summary>
    public const string SurfaceDecidedEntrypoint = "xmip_surface_decided_v1";

    /// <summary>The files a remote surface presents, checked.</summary>
    public const string SurfaceNamedEntrypoint = "xmip_surface_named_v1";

    /// <summary>What a list of failed Messages that is none says.</summary>
    public const string SurfaceUnlistedEntrypoint = "xmip_surface_unlisted_v1";

    /// <summary>The node and the Send Port a scope is at.</summary>
    public const string SurfacePortAtEntrypoint = "xmip_surface_port_at_v1";

    /// <summary>Why <c>--remote</c> with no address is refused.</summary>
    public const string SurfaceRemoteRefusalEntrypoint = "xmip_surface_remote_refusal_v1";

    /// <summary>Why <c>--local</c> with no endpoint is refused.</summary>
    public const string SurfaceLocalRefusalEntrypoint = "xmip_surface_local_refusal_v1";

    /// <summary>Why a snapshot takes no act.</summary>
    public const string SurfaceNotActedEntrypoint = "xmip_surface_not_acted_v1";

    /// <summary>What a local surface naming no node says.</summary>
    public const string SurfaceUnnamedEntrypoint = "xmip_surface_unnamed_v1";
}
