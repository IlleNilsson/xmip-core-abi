namespace Xmip.Abi.Operate;

// Section 7's symbols: the rules a surface asks the runtime rather than
// writing again — scopes, stages, roles, moods, counted kinds and a
// declared capability (ADR-0027, amendment 2026-09-24).
public static partial class OperateAbi
{
    /// <summary>Whether one scope is at or beneath another: section 7,
    /// forwarded to <c>observe::Scope::contains</c>.</summary>
    public const string ScopeContainsEntrypoint = "xmip_scope_contains_v1";

    /// <summary>Whether a scope is what a pattern names by the one wildcard:
    /// section 7, forwarded to <c>observe::wildcard::matches</c>.</summary>
    public const string ScopeMatchesEntrypoint = "xmip_scope_matches_v1";

    /// <summary>Whether what a run made is shown, as the run declared and the
    /// reader asked: section 7, <c>observe::run::shown</c>.</summary>
    public const string RunShownEntrypoint = "xmip_run_shown_v1";

    /// <summary>A scope's segments: section 7, <c>observe::Scope::segments</c>.</summary>
    public const string ScopePartsEntrypoint = "xmip_scope_parts_v1";

    /// <summary>The node a scope is on and its stage there: section 7,
    /// <c>observe::Scope::node</c> and <c>observe::Scope::stage</c>.</summary>
    public const string ScopeNodeEntrypoint = "xmip_scope_node_v1";

    /// <summary>The stage words: section 7, <c>node::Stage::WORDS</c>.</summary>
    public const string StageWordsEntrypoint = "xmip_stage_words_v1";

    /// <summary>The role words: section 7, <c>node::NodeRole::WORDS</c>.</summary>
    public const string RoleWordsEntrypoint = "xmip_role_words_v1";

    /// <summary>A node's declared roles: section 7,
    /// <c>node::NodeRole::declared</c>.</summary>
    public const string RoleDeclaredEntrypoint = "xmip_role_declared_v1";

    /// <summary>The stages a role serves: section 7,
    /// <c>node::NodeRole::stages</c>.</summary>
    public const string RoleStagesEntrypoint = "xmip_role_stages_v1";

    /// <summary>What a node declaring some roles is for: section 7,
    /// <c>node::NodeRole::purpose</c> (ADR-0056, amendment 2026-11-13).</summary>
    public const string RolePurposeEntrypoint = "xmip_role_purpose_v1";

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

    /// <summary>A stage's title, what a person reads — Receive, Work Process,
    /// Send: section 7, <c>node::Stage::title</c> (ADR-0027, amendment
    /// 2026-11-11).</summary>
    public const string StageTitleEntrypoint = "xmip_stage_title_v1";

    /// <summary>Where the work at a scope of a read publication came from and
    /// went: section 8, <c>observe::topology::from_to</c> (ADR-0027, amendment
    /// 2026-11-11).</summary>
    public const string PublicationFromToEntrypoint = "xmip_publication_from_to_v1";

    /// <summary>Where each thing a read publication draws beneath a focus
    /// stands on the map: section 8, <c>observe::topology::layout</c>
    /// (ADR-0052 and ADR-0056, amendments 2026-11-13).</summary>
    public const string PublicationLayoutEntrypoint = "xmip_publication_layout_v1";

    /// <summary>What a published capability record declares: section 7,
    /// <c>observe::capability::declared</c>.</summary>
    public const string CapabilityPublishedEntrypoint = "xmip_capability_published_v1";

    /// <summary>A run's entry for a node: section 7,
    /// <c>node::Capability::from_entry</c>.</summary>
    public const string CapabilityEntryEntrypoint = "xmip_capability_entry_v1";
}
