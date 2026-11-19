namespace Xmip.Abi.Operate;

// Section 28.1's symbols: a publication's scope tree (ADR-0052, amendment
// 2026-11-17).
public static partial class OperateAbi
{
    /// <summary>An index built from records and counts.</summary>
    public const string ScopeIndexBuildEntrypoint = "xmip_scope_index_build_v1";

    /// <summary>An index built from a read publication.</summary>
    public const string ScopeIndexOfPublicationEntrypoint = "xmip_scope_index_of_publication_v1";

    /// <summary>An index released.</summary>
    public const string ScopeIndexFreeEntrypoint = "xmip_scope_index_free_v1";

    /// <summary>What an index is.</summary>
    public const string ScopeIndexFactsEntrypoint = "xmip_scope_index_facts_v1";

    /// <summary>One scope as the index holds it.</summary>
    public const string ScopeIndexPlacedEntrypoint = "xmip_scope_index_placed_v1";

    /// <summary>The scopes directly beneath one.</summary>
    public const string ScopeIndexChildrenEntrypoint = "xmip_scope_index_children_v1";

    /// <summary>A list of the index's scopes or names.</summary>
    public const string ScopeIndexListEntrypoint = "xmip_scope_index_list_v1";

    /// <summary>What a person reads a scope as.</summary>
    public const string ScopeIndexLabelEntrypoint = "xmip_scope_index_label_v1";

    /// <summary>The place of the worst record on a stage.</summary>
    public const string ScopeIndexWorstAtStageEntrypoint = "xmip_scope_index_worst_at_stage_v1";

    /// <summary>One figure at a scope, as a measurement.</summary>
    public const string ScopeIndexMeasureEntrypoint = "xmip_scope_index_measure_v1";

    /// <summary>Every node's declaration, as JSON.</summary>
    public const string ScopeIndexCapabilitiesEntrypoint = "xmip_scope_index_capabilities_v1";

    /// <summary>What a pattern selects among the index's scopes.</summary>
    public const string ScopeIndexSelectEntrypoint = "xmip_scope_index_select_v1";

    /// <summary>A scope's parent, its node's own scope or its name.</summary>
    public const string ScopeRelativeEntrypoint = "xmip_scope_relative_v1";

    /// <summary>Figures summed.</summary>
    public const string FiguresSumEntrypoint = "xmip_figures_sum_v1";

    /// <summary><c>XMIP_SCOPE_LIST_STAGE_SCOPES</c>.</summary>
    public const uint ScopeListStageScopes = 1;

    /// <summary><c>XMIP_SCOPE_LIST_NODES</c>.</summary>
    public const uint ScopeListNodes = 2;

    /// <summary><c>XMIP_SCOPE_LIST_MATCHING</c>.</summary>
    public const uint ScopeListMatching = 3;

    /// <summary><c>XMIP_SCOPE_RELATIVE_PARENT</c>.</summary>
    public const uint ScopeRelativeParent = 1;

    /// <summary><c>XMIP_SCOPE_RELATIVE_NODE_SCOPE</c>.</summary>
    public const uint ScopeRelativeNodeScope = 2;

    /// <summary><c>XMIP_SCOPE_RELATIVE_NAME</c>.</summary>
    public const uint ScopeRelativeName = 3;
}
