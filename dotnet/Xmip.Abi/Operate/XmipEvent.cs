using System.Runtime.InteropServices;
using Xmip.Abi.Module;

namespace Xmip.Abi.Operate;

/// <summary><c>XmipEvent</c>, section 11 of <c>xmip_operate.h</c>: one Event,
/// references and never a payload. Identifiers are UUIDs in 8-4-4-4-12 form,
/// and every optional string is empty where there is none.</summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct XmipEvent
{
    /// <summary><c>id</c>.</summary>
    public XmipStr Id;

    /// <summary><c>type</c>.</summary>
    public XmipStr Type;

    /// <summary><c>time_unix_nanos</c>.</summary>
    public long TimeUnixNanos;

    /// <summary><c>action</c>, an <c>XmipAction</c>.</summary>
    public int Action;

    /// <summary><c>outcome</c>, an <c>XmipOutcome</c>.</summary>
    public int Outcome;

    /// <summary><c>scope</c>, where it happened.</summary>
    public XmipStr Scope;

    /// <summary><c>journey</c>.</summary>
    public XmipStr Journey;

    /// <summary><c>message</c>.</summary>
    public XmipStr Message;

    /// <summary><c>stream</c>.</summary>
    public XmipStr Stream;

    /// <summary><c>endpoint</c>.</summary>
    public XmipStr Endpoint;

    /// <summary><c>module</c>.</summary>
    public XmipStr Module;

    /// <summary><c>artifact</c>.</summary>
    public XmipStr Artifact;

    /// <summary><c>party</c>, the Party it is about.</summary>
    public XmipStr Party;

    /// <summary><c>diagnostics</c>: name then value.</summary>
    public XmipStr* Diagnostics;

    /// <summary><c>diagnostics_len</c>, even.</summary>
    public nuint DiagnosticsLen;
}
