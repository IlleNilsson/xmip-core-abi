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
public static class OperateAbi
{
    /// <summary>The operator boundary version this build speaks. Not the
    /// module boundary's version, on purpose.</summary>
    public const uint Version = 1u;

    /// <summary>The one symbol a runtime exports for the surfaces that watch.
    /// Section 1 of the header.</summary>
    public const string Entrypoint = "xmip_operate_v1";

    /// <summary>
    /// Wait until the runtime publishes a revision newer than the caller's.
    /// Separate from the table so version 1 remains binary-compatible.
    /// </summary>
    public const string ChangeEntrypoint = "xmip_wait_change_v1";

    /// <summary>Start a node from a saved configuration file. Section 6 of the
    /// header; a configurer's symbol, not part of the watcher's table.</summary>
    public const string StartEntrypoint = "xmip_start_v1";

    /// <summary>Validate configuration text without applying it. Section 6 of
    /// the header; ADR-0027 clause 9.</summary>
    public const string ValidateEntrypoint = "xmip_validate_v1";

    /// <summary>Whether one scope is at or beneath another: section 7,
    /// forwarded to <c>observe::Scope::contains</c>.</summary>
    public const string ScopeContainsEntrypoint = "xmip_scope_contains_v1";

    /// <summary>A scope's segments: section 7, <c>observe::Scope::segments</c>.</summary>
    public const string ScopePartsEntrypoint = "xmip_scope_parts_v1";

    /// <summary>The stage words: section 7, <c>node::Stage::WORDS</c>.</summary>
    public const string StageWordsEntrypoint = "xmip_stage_words_v1";

    /// <summary>A node's declared stages: section 7,
    /// <c>node::Stage::declared</c>.</summary>
    public const string StageDeclaredEntrypoint = "xmip_stage_declared_v1";

    /// <summary>A mood's word: section 7, <c>observe::Health::word</c>.</summary>
    public const string HealthWordEntrypoint = "xmip_health_word_v1";

    /// <summary>A mood's color name: section 7, <c>observe::Health::color</c>.</summary>
    public const string HealthColorEntrypoint = "xmip_health_color_v1";

    /// <summary>The mood a word names: section 7, <c>observe::Health::named</c>.</summary>
    public const string HealthNamedEntrypoint = "xmip_health_named_v1";

    /// <summary>Records in the worst-first order: section 7,
    /// <c>observe::Standing::worst_first</c>.</summary>
    public const string HealthOrderEntrypoint = "xmip_health_order_v1";

    /// <summary>What a parent shows over a mood: section 7,
    /// <c>observe::Health::rolled</c>.</summary>
    public const string HealthRolledEntrypoint = "xmip_health_rolled_v1";

    /// <summary>A counted kind's word: section 7,
    /// <c>observe::Counted::word</c>.</summary>
    public const string CountedWordEntrypoint = "xmip_counted_word_v1";

    /// <summary>What a stage counts: section 7, <c>observe::Counted::at</c>.</summary>
    public const string StageCountedEntrypoint = "xmip_stage_counted_v1";

    /// <summary>Whether a stage may be paused: section 7,
    /// <c>node::Stage::pausable</c>.</summary>
    public const string StagePausableEntrypoint = "xmip_stage_pausable_v1";

    /// <summary>What a thing at a stage is called: section 7,
    /// <c>node::Stage::location</c>.</summary>
    public const string StageLocationEntrypoint = "xmip_stage_location_v1";

    /// <summary>What a published capability record declares: section 7,
    /// <c>observe::capability::declared</c>.</summary>
    public const string CapabilityPublishedEntrypoint = "xmip_capability_published_v1";

    /// <summary>A run's entry for a node: section 7,
    /// <c>node::Capability::from_entry</c>.</summary>
    public const string CapabilityEntryEntrypoint = "xmip_capability_entry_v1";

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
}
