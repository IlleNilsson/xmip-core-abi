using System.Runtime.InteropServices;
using System.Text;
using Xmip.Abi.Module;

namespace Xmip.Abi.Operate;

/// <summary>
/// Section 9 of <c>include/xmip_operate.h</c>, crossed by P/Invoke: a
/// program's audit record, recorded by the audit capability
/// (<c>xmip-core-audit</c>'s <c>ProgramAudit</c>) through the runtime's
/// library. The record, its policy, the file sink and the fallback to the
/// operating system's log are the capability's; this binds the one call once
/// for every .NET program (ADR-0062 clause 2).
/// </summary>
/// <remarks>
/// Pure in the header's sense: it reads no snapshot and holds nothing, so one
/// instance serves a whole process from any thread.
/// </remarks>
public sealed unsafe class RuntimeAudit
{
    // Where a record went and why fits in this. A longer sentence is cut
    // here rather than asked for again, because asking again would record
    // the act twice.
    private const int Said = 4096;

    private readonly delegate* unmanaged[Cdecl]<
        XmipStr, XmipStr, XmipStr, int, int, XmipStr, XmipStr*, nuint, int*, byte*, nuint,
        nuint*, int> _audit;

    internal RuntimeAudit(nint library)
    {
        _audit = (delegate* unmanaged[Cdecl]<
            XmipStr, XmipStr, XmipStr, int, int, XmipStr, XmipStr*, nuint, int*, byte*, nuint,
            nuint*, int>)
            NativeLibrary.GetExport(library, OperateAbi.AuditEntrypoint);
    }

    /// <summary>Section 9's symbol, which a runtime must export.</summary>
    public static IReadOnlyList<string> Entrypoints { get; } = [OperateAbi.AuditEntrypoint];

    /// <summary>
    /// Record one act of <paramref name="program"/>. <paramref name="directory"/>
    /// is where its records go when the program was told one; null or empty
    /// lets the capability decide.
    /// </summary>
    /// <exception cref="ArgumentException">A phase or severity the header does
    /// not define.</exception>
    public AuditOutcome Record(
        string program,
        string? directory,
        string action,
        AuditPhase phase,
        AuditSeverity severity,
        string? message,
        IReadOnlyDictionary<string, string>? properties)
    {
        ArgumentNullException.ThrowIfNull(program);
        ArgumentNullException.ThrowIfNull(action);

        // Every string in one buffer, each crossing as its own span of it.
        List<string> texts = [program, directory ?? string.Empty, action, message ?? string.Empty];

        foreach ((string key, string value) in properties ?? new Dictionary<string, string>())
        {
            texts.Add(key);
            texts.Add(value);
        }

        int[] starts = new int[texts.Count + 1];
        byte[] bytes = new byte[texts.Sum(Encoding.UTF8.GetByteCount)];

        for (int i = 0; i < texts.Count; i++)
        {
            starts[i + 1] = starts[i] + Encoding.UTF8.GetBytes(texts[i], 0, texts[i].Length,
                bytes, starts[i]);
        }

        // The first four are program, directory, action and message; the rest
        // are the properties, key then value, as the header lays them out.
        XmipStr[] strings = new XmipStr[texts.Count];
        byte[] said = new byte[Said];
        nuint saidLength = 0;
        int kept = -1;
        int status;

        fixed (byte* data = bytes)
        fixed (XmipStr* each = strings)
        fixed (byte* sentence = said)
        {
            for (int i = 0; i < texts.Count; i++)
            {
                each[i] = new XmipStr(data + starts[i], (nuint)(starts[i + 1] - starts[i]));
            }

            status = _audit(each[0], each[1], each[2], (int)phase, (int)severity, each[3],
                each + 4, (nuint)(texts.Count - 4), &kept, sentence, (nuint)said.Length,
                &saidLength);
        }

        if (status == (int)XmipStatus.Invalid)
        {
            throw new ArgumentException(
                $"the runtime refused phase {phase} or severity {severity}");
        }

        string words = Encoding.UTF8.GetString(
            said, 0, (int)Math.Min(saidLength, (nuint)said.Length));

        return status == 0
            ? new AuditOutcome((AuditKept)kept, words)
            : new AuditOutcome(null, words);
    }
}
