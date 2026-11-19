using System.Runtime.InteropServices;
using Xmip.Abi.Module;

namespace Xmip.Abi.Operate;

/// <summary><c>XmipStatusMeaning</c>, section 28.3 of <c>xmip_operate.h</c>:
/// what a status code means, every text static.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct XmipStatusMeaning
{
    /// <summary><c>known</c>: 1 where the header defines the code.</summary>
    public byte Known;

    /// <summary><c>name</c>: the header's name, or <c>unknown</c>.</summary>
    public XmipStr Name;

    /// <summary><c>meaning</c>: one line an operator can read.</summary>
    public XmipStr Meaning;

    /// <summary><c>retryable</c>.</summary>
    public byte Retryable;

    /// <summary><c>terminal</c>.</summary>
    public byte Terminal;
}
