namespace Xmip.Abi.Operate;

/// <summary>The lists of a published run, as <c>XmipRunList</c> in section 8
/// of <c>xmip_operate.h</c> numbers them.</summary>
public enum RunList
{
    /// <summary>The tests that run.</summary>
    Tests = 0,

    /// <summary>The nodes spawned.</summary>
    Nodes = 1,

    /// <summary>What each node was started with, as entries.</summary>
    Capabilities = 2,

    /// <summary>The nodes that may assume the internet.</summary>
    Online = 3,
}
