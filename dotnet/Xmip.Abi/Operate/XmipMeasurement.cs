using System.Runtime.InteropServices;
using Xmip.Abi.Module;

namespace Xmip.Abi.Operate;

/// <summary>
/// One measurement as it crosses: a scope, what was counted, the value, the
/// window the value covers, and when it was taken. Section 4 of
/// <c>xmip_operate.h</c>; ADR-0027 clause 5, never a bare number.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct XmipMeasurement
{
    /// <summary>The Xmip URI this measurement is about.</summary>
    public XmipStr Scope;

    /// <summary>What was counted, as an <c>int</c>; see <see cref="Counted"/>.</summary>
    public int Counted;

    /// <summary>The value, summed over the scope tree.</summary>
    public ulong Value;

    /// <summary>Where the window opens.</summary>
    public long WindowStartUnixNanos;

    /// <summary>Where the window closes.</summary>
    public long WindowEndUnixNanos;

    /// <summary>When the runtime took the snapshot.</summary>
    public long ObservedUnixNanos;
}
