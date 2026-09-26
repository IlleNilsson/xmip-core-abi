using System.Runtime.InteropServices;
using Xmip.Abi.Module;

namespace Xmip.Abi.Operate;

/// <summary><c>XmipEventFilter</c>, section 11 of <c>xmip_operate.h</c>: which
/// Events a subscription asks for, each list and string empty for
/// any.</summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct XmipEventFilter
{
    /// <summary><c>types</c>.</summary>
    public XmipStr* Types;

    /// <summary><c>types_len</c>.</summary>
    public nuint TypesLen;

    /// <summary><c>outcomes</c>, each an <c>XmipOutcome</c>.</summary>
    public int* Outcomes;

    /// <summary><c>outcomes_len</c>.</summary>
    public nuint OutcomesLen;

    /// <summary><c>scope</c>: at or beneath it.</summary>
    public XmipStr Scope;

    /// <summary><c>party</c>: the Party's UUID an Event must be about.</summary>
    public XmipStr Party;
}
