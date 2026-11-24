namespace Xmip.Abi.Operate;

/// <summary>The lists of a published run, as <c>XmipRunList</c> in section 8
/// of <c>xmip_operate.h</c> numbers them.</summary>
public enum RunList
{
    /// <summary>The tests that run.</summary>
    Tests = 0,

    /// <summary>The nodes spawned.</summary>
    Nodes = 1,

    /// <summary>The roles each node was started with, as entries.</summary>
    Roles = 2,

    /// <summary>Each node's Node capabilities, <c>&lt;node&gt;=online+erp-direct</c>
    /// (ADR-0056, amendment 2026-11-24).</summary>
    Capabilities = 3,

    /// <summary>The nodes on machines of their own, never spawned: every
    /// other node is simulated.</summary>
    Real = 4,
}
