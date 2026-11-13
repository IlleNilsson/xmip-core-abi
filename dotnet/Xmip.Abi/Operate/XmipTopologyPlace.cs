using System.Runtime.InteropServices;
using Xmip.Abi.Module;

namespace Xmip.Abi.Operate;

/// <summary><c>XmipTopologyPlace</c>, section 8 of <c>xmip_operate.h</c>:
/// where one drawn thing stands on the map, as
/// <c>xmip_publication_layout_v1</c> lays it out. Its id borrows from the
/// publication.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct XmipTopologyPlace
{
    /// <summary><c>id</c>, a node id of the publication.</summary>
    public XmipStr Id;

    /// <summary><c>x</c>, from 0 at the left to 1 at the right.</summary>
    public double X;

    /// <summary><c>y</c>, from 0 at the top to 1 at the bottom.</summary>
    public double Y;

    /// <summary><c>band</c>, an <c>XmipTopologyBand</c>.</summary>
    public int Band;

    /// <summary><c>column</c>, an <c>XmipTopologyColumn</c>.</summary>
    public int Column;
}
