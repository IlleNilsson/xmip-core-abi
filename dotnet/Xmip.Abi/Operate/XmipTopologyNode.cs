using System.Runtime.InteropServices;
using Xmip.Abi.Module;

namespace Xmip.Abi.Operate;

/// <summary><c>XmipTopologyNode</c>, section 8 of <c>xmip_operate.h</c>: one
/// thing that communicates, as the runtime's publication reader lays it
/// out.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct XmipTopologyNode
{
    /// <summary><c>id</c>.</summary>
    public XmipStr Id;

    /// <summary><c>parent</c>; empty at the top.</summary>
    public XmipStr Parent;

    /// <summary><c>label</c>.</summary>
    public XmipStr Label;

    /// <summary><c>kind</c>, an <c>XmipTopologyKind</c>.</summary>
    public int Kind;

    /// <summary><c>scope</c>.</summary>
    public XmipStr Scope;

    /// <summary><c>health</c>, an <c>XmipHealth</c>.</summary>
    public int Health;

    /// <summary><c>origin</c>, an <c>XmipTopologyOrigin</c>.</summary>
    public int Origin;

    /// <summary><c>load</c>, zero to one.</summary>
    public double Load;

    /// <summary><c>activity</c>, zero to one.</summary>
    public double Activity;

    /// <summary><c>evidence</c>.</summary>
    public XmipStr Evidence;
}
