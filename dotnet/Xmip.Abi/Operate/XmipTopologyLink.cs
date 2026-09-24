using System.Runtime.InteropServices;
using Xmip.Abi.Module;

namespace Xmip.Abi.Operate;

/// <summary><c>XmipTopologyLink</c>, section 8 of <c>xmip_operate.h</c>: one
/// communication relationship, as the runtime's publication reader lays it
/// out.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct XmipTopologyLink
{
    /// <summary><c>id</c>.</summary>
    public XmipStr Id;

    /// <summary><c>from</c>, a node id.</summary>
    public XmipStr From;

    /// <summary><c>to</c>, a node id.</summary>
    public XmipStr To;

    /// <summary><c>pattern</c>, an <c>XmipCommunicationPattern</c>.</summary>
    public int Pattern;

    /// <summary><c>origin</c>, an <c>XmipTopologyOrigin</c>.</summary>
    public int Origin;

    /// <summary><c>protocol</c>.</summary>
    public XmipStr Protocol;

    /// <summary><c>health</c>, an <c>XmipHealth</c>.</summary>
    public int Health;

    /// <summary><c>volume</c>.</summary>
    public ulong Volume;

    /// <summary><c>rate</c>.</summary>
    public double Rate;

    /// <summary><c>latency_ms</c>.</summary>
    public double LatencyMilliseconds;

    /// <summary><c>progress</c>, zero to one.</summary>
    public double Progress;

    /// <summary><c>attempts</c>.</summary>
    public uint Attempts;

    /// <summary><c>evidence</c>.</summary>
    public XmipStr Evidence;
}
