namespace Xmip.Abi.Operate;

/// <summary>
/// The Xmip operator boundary, as declared by <c>include/xmip_operate.h</c> in
/// xmip-core-abi: its version and the symbols a runtime exports.
/// </summary>
/// <remarks>
/// ADR-0027: the boundary that drives Xmip from outside, versioned apart from
/// the module boundary a Module plugs into. ADR-0012 clause 1 applies here
/// too: the header is normative and this is not; where they differ, the
/// header is right and this is a defect.
/// </remarks>
public static partial class OperateAbi
{
    /// <summary>The operator boundary version this build speaks. Not the
    /// module boundary's version, on purpose.</summary>
    public const uint Version = 1u;

    /// <summary>The one symbol a runtime exports for the surfaces that watch.
    /// Section 1 of the header.</summary>
    public const string Entrypoint = "xmip_operate_v1";

    /// <summary>Start a node from a saved configuration file. Section 6 of the
    /// header; a configurer's symbol, not part of the watcher's table.</summary>
    public const string StartEntrypoint = "xmip_start_v1";

    /// <summary>Validate configuration text without applying it. Section 6 of
    /// the header; ADR-0027 clause 9.</summary>
    public const string ValidateEntrypoint = "xmip_validate_v1";

    /// <summary>Read a publication: section 8,
    /// <c>observe::Publication::read</c>.</summary>
    public const string PublicationReadEntrypoint = "xmip_publication_read_v1";

    /// <summary>Release a read publication: section 8.</summary>
    public const string PublicationFreeEntrypoint = "xmip_publication_free_v1";

    /// <summary>A read publication's head: section 8.</summary>
    public const string PublicationHeadEntrypoint = "xmip_publication_head_v1";

    /// <summary>A read publication's records: section 8.</summary>
    public const string PublicationRecordsEntrypoint = "xmip_publication_records_v1";

    /// <summary>A read publication's counts: section 8.</summary>
    public const string PublicationCountsEntrypoint = "xmip_publication_counts_v1";

    /// <summary>A read publication's topology nodes: section 8.</summary>
    public const string PublicationNodesEntrypoint = "xmip_publication_nodes_v1";

    /// <summary>A read publication's topology links: section 8.</summary>
    public const string PublicationLinksEntrypoint = "xmip_publication_links_v1";

    /// <summary>One of a read publication's run lists: section 8.</summary>
    public const string PublicationRunEntrypoint = "xmip_publication_run_v1";

    /// <summary>What a topology kind is called, its word and its name:
    /// section 8, <c>observe::NodeKind</c>.</summary>
    public const string TopologyKindWordsEntrypoint = "xmip_topology_kind_words_v1";

    /// <summary>What a topology origin is called: section 8,
    /// <c>observe::Origin</c>.</summary>
    public const string TopologyOriginWordsEntrypoint = "xmip_topology_origin_words_v1";

    /// <summary>What a communication pattern is called: section 8,
    /// <c>observe::Pattern</c>.</summary>
    public const string TopologyPatternWordsEntrypoint = "xmip_topology_pattern_words_v1";

    /// <summary>Read a curve, a node's throughput over time: section 8,
    /// <c>observe::Curve::read</c>.</summary>
    public const string CurveReadEntrypoint = "xmip_curve_read_v1";

    /// <summary>A read curve's points: section 8.</summary>
    public const string CurvePointsEntrypoint = "xmip_curve_points_v1";

    /// <summary>Release a read curve: section 8.</summary>
    public const string CurveFreeEntrypoint = "xmip_curve_free_v1";

    /// <summary>Record one act of a program: section 9, forwarded to
    /// <c>xmip-core-audit</c>'s <c>ProgramAudit::record</c> (ADR-0062).</summary>
    public const string AuditEntrypoint = "xmip_audit_v1";

    /// <summary>The audit read back: section 9, forwarded to
    /// <c>xmip-core-audit</c>'s reader and <c>AuditQuery</c> (ADR-0062,
    /// amendment 2026-09-29).</summary>
    public const string AuditReadEntrypoint = "xmip_audit_read_v1";

    /// <summary>The Windows Event Log source every Xmip entry is written under
    /// when audit cannot persist a record: section 9's
    /// <c>XMIP_EVENT_SOURCE</c> (ADR-0062 clause 3).</summary>
    public const string EventSource = "Xmip";

    /// <summary>The sentence an entry opens with when <see cref="EventSource"/>
    /// is not registered and the entry goes under <c>.NET Runtime</c>: section
    /// 9's <c>XMIP_EVENT_SOURCE_UNREGISTERED</c>.</summary>
    public const string EventSourceUnregistered =
        "The Xmip event source is not registered and registering it needs elevation once " +
        "(Install-XmipPrerequisite does it), so this is written under the .NET Runtime source.";

    /// <summary>A cluster's <c>cluster.xmip</c>, one view per artifact kind:
    /// section 10, forwarded to <c>configure::views::Views::of</c>.</summary>
    public const string ClusterViewsEntrypoint = "xmip_cluster_views_v1";

    /// <summary>A filter's text as rows and groups: section 10,
    /// <c>configure::filter::structure</c>.</summary>
    public const string FilterStructureEntrypoint = "xmip_filter_structure_v1";

    /// <summary>Rows and groups as a filter's canonical text: section 10,
    /// <c>configure::filter::text</c>.</summary>
    public const string FilterTextEntrypoint = "xmip_filter_text_v1";

    /// <summary>A cluster's <c>cluster.xmip</c> with one edit made: section 10,
    /// <c>configure::view_edit::apply</c>.</summary>
    public const string ClusterEditEntrypoint = "xmip_cluster_edit_v1";

    /// <summary>Each node's document, sliced from a cluster's
    /// <c>cluster.xmip</c> by the one slicing: section 10,
    /// <c>configure::slices</c> (ADR-0031, amendment 2026-10-05).</summary>
    public const string ClusterSlicesEntrypoint = "xmip_cluster_slices_v1";

    /// <summary>One node's document sliced from a cluster's <c>cluster.xmip</c>
    /// and signed by the cluster's authority beside that file, with the
    /// authority's certificate: section 10, <c>design::signed_slice</c>
    /// (ADR-0031, amendment 2026-11-06).</summary>
    public const string ClusterSignedSliceEntrypoint = "xmip_cluster_signed_slice_v1";

    /// <summary>Subscribe to the process's Events with a filter: section 11,
    /// forwarded to <c>xmip-core-event</c>'s <c>Hub::subscribe</c> (ADR-0065)
    /// as the Party the process's account proves to be, never one it names
    /// (ADR-0019, amendment 2026-11-11).</summary>
    public const string EventSubscribeEntrypoint = "xmip_event_subscribe_v1";

    /// <summary>Drain a subscription's queue as a batch: section 11.</summary>
    public const string EventNextEntrypoint = "xmip_event_next_v1";

    /// <summary>Release a drained batch: section 11.</summary>
    public const string EventBatchFreeEntrypoint = "xmip_event_batch_free_v1";

    /// <summary>Subscribe and be called back for each Event: section 11.</summary>
    public const string EventListenEntrypoint = "xmip_event_listen_v1";

    /// <summary>Release either kind of subscription: section 11.</summary>
    public const string EventUnsubscribeEntrypoint = "xmip_event_unsubscribe_v1";

    /// <summary>Publish an Event of the caller's: section 11,
    /// <c>Hub::publish</c>.</summary>
    public const string EventPublishEntrypoint = "xmip_event_publish_v1";

    /// <summary>Hand this process's hub the policy of who may subscribe:
    /// section 11, <c>XMIP_EVENT_AUTHORIZE_ENTRYPOINT</c>.</summary>
    public const string EventAuthorizeEntrypoint = "xmip_event_authorize_v1";

    /// <summary>Who was not heard when a batch was drained: section 11,
    /// <c>XMIP_EVENT_BATCH_UNHEARD_ENTRYPOINT</c>.</summary>
    public const string EventBatchUnheardEntrypoint = "xmip_event_batch_unheard_v1";

    /// <summary>Who this process's hub does not hear now: section 11,
    /// <c>XMIP_EVENT_UNHEARD_ENTRYPOINT</c>.</summary>
    public const string EventUnheardEntrypoint = "xmip_event_unheard_v1";

    /// <summary>The Event subscriptions this process's hub holds: section
    /// 11 (ADR-0065, amendment 2026-09-29), <c>Hub::standing</c>.</summary>
    public const string EventSubscriptionsEntrypoint = "xmip_event_subscriptions_v1";

    /// <summary>Pause, resume or remove one of them: section 11,
    /// <c>Hub::act</c>.</summary>
    public const string EventSubscriptionActEntrypoint = "xmip_event_subscription_act_v1";

    /// <summary>The Event subscriptions a read publication carries: section
    /// 11 over section 8's handle.</summary>
    public const string PublicationEventSubscriptionsEntrypoint =
        "xmip_publication_event_subscriptions_v1";

    /// <summary>The Subscriptions of every node running in this process:
    /// section 14 (ADR-0013, amendment 2026-09-30).</summary>
    public const string SubscriptionsEntrypoint = "xmip_subscriptions_v1";

    /// <summary>Pause or resume one of them: section 14. There is no
    /// remove.</summary>
    public const string SubscriptionActEntrypoint = "xmip_subscription_act_v1";

    /// <summary>The Subscriptions a read publication carries: section 14
    /// over section 8's handle.</summary>
    public const string PublicationSubscriptionsEntrypoint = "xmip_publication_subscriptions_v1";

    /// <summary>An act on a Subscription, an Event subscription, a Message
    /// in a Dead Message Queue or a Message that failed, left for a node read
    /// through its publication: section 14, <c>observe::Order::leave</c>.</summary>
    public const string OrderEntrypoint = "xmip_order_v1";

    /// <summary>The Dead Message Queues of every node running in this
    /// process: section 15 (ADR-0052, amendment 2026-10-01).</summary>
    public const string DeadMessagesEntrypoint = "xmip_dead_messages_v1";

    /// <summary>Replay one Message from a Dead Message Queue: section
    /// 15.</summary>
    public const string DeadMessageReplayEntrypoint = "xmip_dead_message_replay_v1";

    /// <summary>The Dead Message Queue entries a read publication carries:
    /// section 15 over section 8's handle.</summary>
    public const string PublicationDeadMessagesEntrypoint = "xmip_publication_dead_messages_v1";

    /// <summary>Retry or Dismiss a Message that failed: section 16
    /// (runtime-model.md section 13; ADR-0013).</summary>
    public const string MessageActEntrypoint = "xmip_message_act_v1";

    /// <summary>The Messages that failed at the Send Ports of the nodes
    /// running in this process, read from Xmip Storage a page at a time:
    /// section 16.</summary>
    public const string FailedMessagesEntrypoint = "xmip_failed_messages_v1";

    /// <summary>The Messages that failed a read publication carries: section
    /// 16 over section 8's handle.</summary>
    public const string PublicationFailedMessagesEntrypoint =
        "xmip_publication_failed_messages_v1";

    /// <summary>The technologies the runtime carries and the settings each
    /// declares: section 12 (ADR-0064, amendment 2026-09-26).</summary>
    public const string TechnologyCatalogueEntrypoint = "xmip_technology_catalogue_v1";

    /// <summary>Declare the calling System Process: section 13, forwarded to
    /// <c>xmip-core-node</c>'s <c>Declaration::declare</c> (ADR-0053).</summary>
    public const string ProcessDeclareEntrypoint = "xmip_process_declare_v1";

    /// <summary>The System Process declarations standing in a directory:
    /// section 13, forwarded to <c>xmip-core-node</c>'s <c>standing</c>.</summary>
    public const string ProcessDeclarationsEntrypoint = "xmip_process_declarations_v1";

    /// <summary>A node reached at its operate address: section 17 (ADR-0014,
    /// amendment 2026-10-12).</summary>
    public const string RemoteOpenEntrypoint = "xmip_remote_open_v1";

    /// <summary>A node on this machine reached at its local endpoint: section
    /// 17 (ADR-0014, amendment 2026-10-13).</summary>
    public const string RemoteOpenLocalEntrypoint = "xmip_remote_open_local_v1";

    /// <summary>A node reached at its operate address presenting a key its
    /// platform's keeper holds and signs with: section 17 (ADR-0034,
    /// amendment 2026-10-15).</summary>
    public const string RemoteOpenKeptEntrypoint = "xmip_remote_open_kept_v1";

    /// <summary>The remote node's publication: section 17.</summary>
    public const string RemotePublicationEntrypoint = "xmip_remote_publication_v1";

    /// <summary>The remote node's follow, waited on: section 17.</summary>
    public const string RemoteWaitEntrypoint = "xmip_remote_wait_v1";

    /// <summary>An act asked of the remote node: section 17.</summary>
    public const string RemoteActEntrypoint = "xmip_remote_act_v1";

    /// <summary>The follow ended and every wait woken: section 17.</summary>
    public const string RemoteEndEntrypoint = "xmip_remote_end_v1";

    /// <summary>A secret a Location presents, set through a remote node:
    /// section 24 (ADR-0019, amendment 2026-10-29).</summary>
    public const string SecretEntrypoint = "xmip_secret_v1";

    /// <summary>The secrets a remote node keeps, never a value: section
    /// 24.</summary>
    public const string SecretsEntrypoint = "xmip_secrets_v1";

    /// <summary>A remote node released: section 17.</summary>
    public const string RemoteFreeEntrypoint = "xmip_remote_free_v1";

    /// <summary>An operate listener this process serves: section 17.</summary>
    public const string OperateServeEntrypoint = "xmip_operate_serve_v2";

    /// <summary>An operate listener this process serves on this machine's
    /// local endpoint: section 17.</summary>
    public const string OperateServeLocalEntrypoint = "xmip_operate_serve_local_v2";

    /// <summary>Where a served listener listens: section 17.</summary>
    public const string OperateServedAddressEntrypoint = "xmip_operate_served_address_v1";

    /// <summary>A publication posted to a served listener: section 17.</summary>
    public const string OperatePostEntrypoint = "xmip_operate_post_v1";

    /// <summary>A served listener stopped and released: section 17.</summary>
    public const string OperateServeStopEntrypoint = "xmip_operate_serve_stop_v1";

    /// <summary>The security role a word names: section 18 (ADR-0009).</summary>
    public const string SecurityRoleNamedEntrypoint = "xmip_security_role_named_v1";

    /// <summary>What a security role is called and may do: section 18.</summary>
    public const string SecurityRoleWordsEntrypoint = "xmip_security_role_words_v1";

    /// <summary>The one check every act passes: section 18.</summary>
    public const string ActAdmittedEntrypoint = "xmip_act_admitted_v1";

    /// <summary>A person's key and certificate request made: section 19
    /// (ADR-0034, amendment 2026-10-15).</summary>
    public const string OperatorRequestEntrypoint = "xmip_operator_request_v1";

    /// <summary>What an operator's request signs over a key its platform's
    /// keeper holds: section 19.</summary>
    public const string OperatorInformationEntrypoint = "xmip_operator_information_v1";

    /// <summary>The request the information and the keeper's signature make:
    /// section 19.</summary>
    public const string OperatorAssembleEntrypoint = "xmip_operator_assemble_v1";

    /// <summary>A chain or anchors kept whole at a file, synced: section 19
    /// (ADR-0034, amendment 2026-10-16).</summary>
    public const string OperatorKeepEntrypoint = "xmip_operator_keep_v1";

    /// <summary>A person's request signed by the authority a node's
    /// <c>[provision.operator]</c> names: section 19.</summary>
    public const string OperatorSignEntrypoint = "xmip_operator_sign_v1";

    /// <summary>A node's key and certificate issued by the cluster's own
    /// authority, for a node put on a machine of its own: section 22.</summary>
    public const string NodeIssueEntrypoint = "xmip_node_issue_v1";

    /// <summary>An act on the cluster's authority kept in a folder, or on a
    /// node's certificate carried across an air gap, its arguments and its
    /// answer JSON: section 23 (ADR-0034, amendment 2026-10-22).</summary>
    public const string AuthorityEntrypoint = "xmip_authority_v1";

    /// <summary>The scripts that upgrade each database on a server from one
    /// schema version to a later one: section 20 (ADR-0015, amendment
    /// 2026-10-20).</summary>
    public const string DatabaseUpgradeEntrypoint = "xmip_database_upgrade_v1";

    /// <summary>Every Report listed, and one opened: section 25 (ADR-0072,
    /// amendment 2026-11-10).</summary>
    public const string ReportEntrypoint = "xmip_report_v1";
}
