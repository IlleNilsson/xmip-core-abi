using System.Runtime.InteropServices;
using System.Text;
using Xmip.Abi.Module;

namespace Xmip.Abi.Operate;

/// <summary>
/// Section 10 of <c>include/xmip_operate.h</c>, crossed by P/Invoke: the
/// cluster's <c>xmip.toml</c> read, edited and sliced (ADR-0064, amendment
/// 2026-10-03; ADR-0031, amendment 2026-10-05). The views, an Xmip
/// Application's routes, a filter's text and structure, every edit and the
/// slicing are <c>xmip-core-configure</c>'s, forwarded by the runtime's
/// library; this binds each of the five once for every .NET surface, and the
/// Operation Desktop's Configure page draws what they answer.
/// </summary>
/// <remarks>Pure in the header's sense: one instance serves a whole process
/// from any thread. Every answer is the header's JSON or text, in memory
/// only (ADR-0031 clause 2).</remarks>
public sealed unsafe class RuntimeDesign
{
    // What a cluster's file is in practice, so one call answers; a longer
    // answer is asked for again at its true length.
    private const int Room = 64 * 1024;

    private readonly delegate* unmanaged[Cdecl]<
        XmipStr, XmipStr, byte*, nuint, nuint*, int> _views;
    private readonly delegate* unmanaged[Cdecl]<
        XmipStr, XmipStr, byte*, nuint, nuint*, int> _structure;
    private readonly delegate* unmanaged[Cdecl]<
        XmipStr, XmipStr, byte*, nuint, nuint*, int> _text;
    private readonly delegate* unmanaged[Cdecl]<
        XmipStr, XmipStr, byte*, nuint, nuint*, int> _edit;
    private readonly delegate* unmanaged[Cdecl]<
        XmipStr, XmipStr, byte*, nuint, nuint*, int> _slices;

    internal RuntimeDesign(nint library)
    {
        _views = Export(library, OperateAbi.ClusterViewsEntrypoint);
        _structure = Export(library, OperateAbi.FilterStructureEntrypoint);
        _text = Export(library, OperateAbi.FilterTextEntrypoint);
        _edit = Export(library, OperateAbi.ClusterEditEntrypoint);
        _slices = Export(library, OperateAbi.ClusterSlicesEntrypoint);
    }

    /// <summary>The symbols this binds, each of which a runtime must
    /// export.</summary>
    public static IReadOnlyList<string> Entrypoints { get; } =
    [
        OperateAbi.ClusterViewsEntrypoint,
        OperateAbi.FilterStructureEntrypoint,
        OperateAbi.FilterTextEntrypoint,
        OperateAbi.ClusterEditEntrypoint,
        OperateAbi.ClusterSlicesEntrypoint,
    ];

    /// <summary>The cluster's file <paramref name="cluster"/> as one view per
    /// artifact kind, the header's JSON. False, with the reader's sentence in
    /// <paramref name="answer"/>, when the text is not TOML.</summary>
    public bool TryViews(string cluster, out string answer)
    {
        return Call(_views, cluster, string.Empty, out answer);
    }

    /// <summary>A filter's text as its rows and groups. False, with the
    /// sentence, when it does not compile.</summary>
    public bool TryFilterStructure(string filter, out string answer)
    {
        return Call(_structure, filter, string.Empty, out answer);
    }

    /// <summary>Rows and groups as the filter's canonical text. False, with
    /// the sentence, when they are not a filter's.</summary>
    public bool TryFilterText(string structure, out string answer)
    {
        return Call(_text, structure, string.Empty, out answer);
    }

    /// <summary>The cluster's file with <paramref name="edit"/>, the header's
    /// JSON, made to it: the edited text, everything the edit does not touch
    /// as it was. False, with the refusal in <paramref name="answer"/>, when
    /// the edit names what is not there, is not TOML, or would leave a node
    /// unable to read its slice.</summary>
    public bool TryEdit(string cluster, string edit, out string answer)
    {
        ArgumentNullException.ThrowIfNull(edit);

        return Call(_edit, cluster, edit, out answer);
    }

    /// <summary>Each node's configuration document sliced from the cluster's
    /// file by the one slicing — or the one <paramref name="node"/> names —
    /// as <c>{"slices":[{"node","text"}]}</c>. False, with the refusal, for
    /// a node's own document, a node the cluster does not declare, or a node
    /// that does not slice.</summary>
    public bool TrySlices(string cluster, string? node, out string answer)
    {
        return Call(_slices, cluster, node ?? string.Empty, out answer);
    }

    private static delegate* unmanaged[Cdecl]<
        XmipStr, XmipStr, byte*, nuint, nuint*, int> Export(nint library, string entrypoint)
    {
        return (delegate* unmanaged[Cdecl]<XmipStr, XmipStr, byte*, nuint, nuint*, int>)
            NativeLibrary.GetExport(library, entrypoint);
    }

    // The header's one shape: OK with the answer, INVALID with the refusal,
    // the true length in out_len whether or not it fit.
    private static bool Call(
        delegate* unmanaged[Cdecl]<XmipStr, XmipStr, byte*, nuint, nuint*, int> export,
        string input,
        string argument,
        out string answer)
    {
        ArgumentNullException.ThrowIfNull(input);

        Utf8Pack pack = new([input, argument]);
        byte[] text = new byte[Room];
        nuint needed = 0;
        int status = Once(export, pack, text, ref needed);

        if (needed > (nuint)text.Length)
        {
            text = new byte[(int)needed];
            status = Once(export, pack, text, ref needed);
        }

        answer = Encoding.UTF8.GetString(text, 0, (int)Math.Min(needed, (nuint)text.Length));

        return status switch
        {
            0 => true,
            (int)XmipStatus.Invalid => false,
            _ => throw new InvalidOperationException(
                $"the runtime answered the cluster's xmip.toml with {(XmipStatus)status}"),
        };
    }

    private static int Once(
        delegate* unmanaged[Cdecl]<XmipStr, XmipStr, byte*, nuint, nuint*, int> export,
        Utf8Pack pack,
        byte[] text,
        ref nuint needed)
    {
        fixed (byte* data = pack.Bytes)
        fixed (byte* written = text)
        fixed (nuint* length = &needed)
        {
            return export(
                pack.Borrow(data, 0), pack.Borrow(data, 1), written, (nuint)text.Length, length);
        }
    }
}
