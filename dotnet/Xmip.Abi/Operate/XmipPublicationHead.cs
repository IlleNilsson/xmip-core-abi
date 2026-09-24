using System.Runtime.InteropServices;
using Xmip.Abi.Module;

namespace Xmip.Abi.Operate;

/// <summary><c>XmipPublicationHead</c>, section 8 of <c>xmip_operate.h</c>:
/// who published, at which scope, and the single values of the run and the
/// topology.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct XmipPublicationHead
{
    /// <summary><c>source</c>.</summary>
    public XmipStr Source;

    /// <summary><c>node</c>: the scope the publication publishes at.</summary>
    public XmipStr Node;

    /// <summary><c>has_run</c>: 1 when the publication says its run.</summary>
    public byte HasRun;

    /// <summary><c>cluster</c>.</summary>
    public XmipStr Cluster;

    /// <summary><c>stress</c>.</summary>
    public XmipStr Stress;

    /// <summary><c>has_topology</c>: 1 when the publication draws one.</summary>
    public byte HasTopology;

    /// <summary><c>topology_source</c>.</summary>
    public XmipStr TopologySource;

    /// <summary><c>topology_observed_unix_nanos</c>.</summary>
    public long TopologyObservedUnixNanos;
}
