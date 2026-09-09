using System.Runtime.InteropServices;
using Xmip.Abi.Module;

namespace Xmip.Abi.Operate;

/// <summary>
/// The operator table. Section 5 of <c>xmip_operate.h</c>: what a surface
/// calls, filled by the runtime, held by the surface, and never the other way
/// round. Every reading function fills up to <c>cap</c> entries, reports the
/// true count in <c>out_len</c> whether or not it fit, and returns a status.
/// </summary>
/// <remarks>
/// There is deliberately no "count now" and no "refresh". A surface reads
/// what was published; if it wants fresher numbers it waits for the
/// publisher. ADR-0027 clause 6.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct XmipOperate
{
    /// <summary>The operator boundary version the runtime filled this for.</summary>
    public uint AbiVersion;

    /// <summary>Runtime-private, passed back to every function.</summary>
    public void* Ctx;

    /// <summary>Health for the scope and everything beneath it, worst first.</summary>
    public delegate* unmanaged[Cdecl]<
        void*, XmipStr, XmipHealthEntry*, nuint, nuint*, int> Health;

    /// <summary>Measurements for the scope, one per counted kind that has a
    /// value.</summary>
    public delegate* unmanaged[Cdecl]<
        void*, XmipStr, int, XmipMeasurement*, nuint, nuint*, int> Measure;

    /// <summary>Pause everything at and beneath a scope, by <c>who</c>. The
    /// first operation on this boundary that acts rather than reads.</summary>
    public delegate* unmanaged[Cdecl]<void*, XmipStr, XmipStr, int> Pause;

    /// <summary>Resume everything at and beneath a scope.</summary>
    public delegate* unmanaged[Cdecl]<void*, XmipStr, int> Resume;

    /// <summary>Release the table. After this, nothing borrowed from it is
    /// valid.</summary>
    public delegate* unmanaged[Cdecl]<void*, void> Destroy;
}
