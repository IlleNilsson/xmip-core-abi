using System.Runtime.InteropServices;
using Xmip.Abi.Module;

namespace Xmip.Abi.Operate;

/// <summary>
/// One scope's health with the evidence behind it, as it crosses. Section 3
/// of <c>xmip_operate.h</c>. Borrowed from the snapshot and valid until the
/// next call on the table, so every string is copied out at once.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct XmipHealthEntry
{
    /// <summary>The Xmip URI this entry is about.</summary>
    public XmipStr Scope;

    /// <summary>The mood, as an <c>int</c>; see <see cref="HealthState"/>.</summary>
    public int Health;

    /// <summary>How far from healthy, 0 to 100, shading the mood within
    /// itself.</summary>
    public byte Severity;

    /// <summary>The one line an operator reads first. May be empty for
    /// <see cref="HealthState.Fine"/>.</summary>
    public XmipStr Evidence;

    /// <summary>When the runtime took the snapshot, not when the surface
    /// asked.</summary>
    public long ObservedUnixNanos;
}
