using System.Runtime.InteropServices;
using System.Text;
using Xmip.Abi.Module;

namespace Xmip.Abi.Operate;

/// <summary>
/// Section 25 of <c>include/xmip_operate.h</c>, crossed by P/Invoke: every
/// Report listed, each saying whether it presents data that already exists
/// or what a Quest probes first, and one opened (ADR-0072, amendment
/// 2026-11-10). The list, each entry's kind, every refusal and every answer
/// are <c>xmip-core-report</c>'s, forwarded by the runtime; this binds the one
/// call once for every .NET surface, so <c>xmip report</c>,
/// <c>Get-XmipReport</c> and the GUI's Reports view read the same answer.
/// </summary>
/// <remarks>
/// Pure in the header's sense: it holds nothing, so one instance serves a
/// whole process from any thread. The answer is the header's JSON, in memory
/// only (ADR-0031 clause 2). A node's build only, as section 23: a remote
/// build leaves it out, and <see cref="RuntimeRules.Reports"/> refuses in
/// words (<see cref="RemoteBuild"/>).
/// </remarks>
public sealed class RuntimeReports
{
    // What a list or an opened Report is in practice, so one call answers; a
    // longer one is asked for again at its true length.
    private const int Room = 64 * 1024;

    private readonly delegate* unmanaged[Cdecl]<XmipStr, XmipStr, byte*, nuint, nuint*, int>
        _report;

    private readonly int _room;

    internal RuntimeReports(nint library)
        : this(
            (delegate* unmanaged[Cdecl]<XmipStr, XmipStr, byte*, nuint, nuint*, int>)
                NativeLibrary.GetExport(library, OperateAbi.ReportEntrypoint),
            Room)
    {
    }

    private RuntimeReports(
        delegate* unmanaged[Cdecl]<XmipStr, XmipStr, byte*, nuint, nuint*, int> report,
        int room)
    {
        _report = report;
        _room = room;
    }

    /// <summary>Section 25's symbol, which a node's build exports.</summary>
    public static IReadOnlyList<string> Entrypoints { get; } = [OperateAbi.ReportEntrypoint];

    /// <summary>The same call, its first ask made with
    /// <paramref name="room"/> bytes: how a test reaches the asking again at
    /// the true length.</summary>
    internal RuntimeReports Starting(int room) => new(_report, room);

    /// <summary>
    /// The act <paramref name="act"/> — <c>list</c> or <c>open</c> — on
    /// <paramref name="arguments"/>, a JSON object of the members the header
    /// names: the runtime's answer, JSON.
    /// </summary>
    /// <exception cref="InvalidOperationException">The runtime did not
    /// answer: its own sentence, opening REFUSED for an act, a Report, a
    /// parameter or a row it does not take, or FAILED where a file could not
    /// be read or the probe's record was kept nowhere; or the status it
    /// answered with otherwise.</exception>
    public string Ask(string act, string arguments)
    {
        ArgumentNullException.ThrowIfNull(act);
        ArgumentNullException.ThrowIfNull(arguments);

        Utf8Pack pack = new([act, arguments]);
        byte[] text = new byte[_room];
        nuint needed = 0;
        int status = Call(pack, text, ref needed);

        // Asked again until it fits: a probe asked again probes again, and
        // may answer a millisecond longer (section 5; section 25 audits only
        // the call that receives it).
        while (needed > (nuint)text.Length)
        {
            text = new byte[(int)needed];
            status = Call(pack, text, ref needed);
        }

        string answer =
            Encoding.UTF8.GetString(text, 0, (int)Math.Min(needed, (nuint)text.Length));

        return status switch
        {
            0 => answer,
            (int)XmipStatus.Invalid or (int)XmipStatus.Io =>
                throw new InvalidOperationException(answer),
            _ => throw new InvalidOperationException(
                $"the runtime answered the Reports with {(XmipStatus)status}"),
        };
    }

    private int Call(Utf8Pack pack, byte[] text, ref nuint needed)
    {
        fixed (byte* data = pack.Bytes)
        fixed (byte* textData = text)
        fixed (nuint* length = &needed)
        {
            unsafe
            {
                return _report(
                    pack.Borrow(data, 0), pack.Borrow(data, 1), textData, (nuint)text.Length,
                    length);
            }
        }
    }
}
