using System.Runtime.InteropServices;
using Xmip.Abi.Module;

namespace Xmip.Abi.Operate;

/// <summary><c>XmipScopePlaced</c>, section 28.1 of <c>xmip_operate.h</c>:
/// one scope as a publication's scope tree holds it, every string and place
/// borrowed from the index.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct XmipScopePlaced
{
    /// <summary><c>scope</c>: as the tree spells it.</summary>
    public XmipStr Scope;

    /// <summary><c>label</c>: the segment it is called by.</summary>
    public XmipStr Label;

    /// <summary><c>leaves</c>: the places of the records at and beneath it,
    /// worst first.</summary>
    public unsafe uint* Leaves;

    /// <summary><c>leaves_len</c>.</summary>
    public nuint LeavesLen;

    /// <summary><c>own</c>: the place of the record published at it, or
    /// -1.</summary>
    public long Own;

    /// <summary><c>mood</c>: a leaf's own, a parent's rollup; -1 for none.</summary>
    public int Mood;

    /// <summary><c>row_mood</c>: the rollup where records lie beneath it,
    /// else its worst record's; -1 for none.</summary>
    public int RowMood;

    /// <summary><c>is_leaf</c>.</summary>
    public byte IsLeaf;

    /// <summary><c>is_container</c>: records lie beneath it, not only at
    /// it.</summary>
    public byte IsContainer;

    /// <summary><c>begins_stage</c>.</summary>
    public byte BeginsStage;

    /// <summary><c>published</c>: the <c>XMIP_FIGURE_*</c> bits of the
    /// figures counted at it.</summary>
    public byte Published;

    /// <summary><c>stage</c>: its stage's word; empty on none.</summary>
    public XmipStr Stage;

    /// <summary><c>streams</c>.</summary>
    public ulong Streams;

    /// <summary><c>messages</c>.</summary>
    public ulong Messages;

    /// <summary><c>journeys</c>.</summary>
    public ulong Journeys;

    /// <summary><c>bytes</c>.</summary>
    public ulong Bytes;

    /// <summary><c>retrying</c>.</summary>
    public ulong Retrying;

    /// <summary><c>failed</c>.</summary>
    public ulong Failed;
}

/// <summary><c>XmipScopeIndexFacts</c>, section 28.1: what an index is.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct XmipScopeIndexFacts
{
    /// <summary><c>revision</c>.</summary>
    public ulong Revision;

    /// <summary><c>leaves</c>: how many records.</summary>
    public nuint Leaves;

    /// <summary><c>scopes</c>: how many scopes.</summary>
    public nuint Scopes;

    /// <summary><c>observed_unix_nanos</c>.</summary>
    public long ObservedUnixNanos;

    /// <summary><c>observed</c>: 1 where the time says anything.</summary>
    public byte Observed;

    /// <summary><c>source</c>, borrowed from the index.</summary>
    public XmipStr Source;
}

/// <summary><c>XmipFigures</c>, section 28.1: six figures and when the
/// newest was observed.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct XmipFigures
{
    /// <summary><c>streams</c>.</summary>
    public ulong Streams;

    /// <summary><c>messages</c>.</summary>
    public ulong Messages;

    /// <summary><c>journeys</c>.</summary>
    public ulong Journeys;

    /// <summary><c>bytes</c>.</summary>
    public ulong Bytes;

    /// <summary><c>retrying</c>.</summary>
    public ulong Retrying;

    /// <summary><c>failed</c>.</summary>
    public ulong Failed;

    /// <summary><c>observed_unix_nanos</c>.</summary>
    public long ObservedUnixNanos;

    /// <summary><c>published</c>: the <c>XMIP_FIGURE_*</c> bits.</summary>
    public byte Published;

    /// <summary><c>observed</c>: 1 where the time says anything.</summary>
    public byte Observed;
}
