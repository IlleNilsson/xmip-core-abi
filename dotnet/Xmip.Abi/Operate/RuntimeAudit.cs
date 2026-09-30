using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Xmip.Abi.Module;

namespace Xmip.Abi.Operate;

/// <summary>
/// Section 9 of <c>include/xmip_operate.h</c>, crossed by P/Invoke: a
/// program's audit record, recorded by the audit capability
/// (<c>xmip-core-audit</c>'s <c>ProgramAudit</c>) through the runtime's
/// library. The record, its policy, the file sink and the fallback to the
/// operating system's log are the capability's; this binds the one call once
/// for every .NET program (ADR-0062 clause 2). <see cref="Read"/> reads the
/// records back through the capability's one reader and query (ADR-0062,
/// amendment 2026-09-29).
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

    // A page of records is asked for again at its true length.
    private const int Room = 256 * 1024;

    private readonly delegate* unmanaged[Cdecl]<XmipStr, XmipStr*, nuint, byte*, nuint, nuint*, int>
        _read;

    internal RuntimeAudit(nint library)
    {
        _read = (delegate* unmanaged[Cdecl]<XmipStr, XmipStr*, nuint, byte*, nuint, nuint*, int>)
            NativeLibrary.GetExport(library, OperateAbi.AuditReadEntrypoint);
        _audit = (delegate* unmanaged[Cdecl]<
            XmipStr, XmipStr, XmipStr, int, int, XmipStr, XmipStr*, nuint, int*, byte*, nuint,
            nuint*, int>)
            NativeLibrary.GetExport(library, OperateAbi.AuditEntrypoint);
    }

    /// <summary>Section 9's symbol, which a runtime must export.</summary>
    public static IReadOnlyList<string> Entrypoints { get; } =
        [OperateAbi.AuditEntrypoint, OperateAbi.AuditReadEntrypoint];

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

        Utf8Pack pack = new(texts);

        // The first four are program, directory, action and message; the rest
        // are the properties, key then value, as the header lays them out.
        XmipStr[] strings = new XmipStr[texts.Count];
        byte[] said = new byte[Said];
        nuint saidLength = 0;
        int kept = -1;
        int status;

        fixed (byte* data = pack.Bytes)
        fixed (XmipStr* each = strings)
        fixed (byte* sentence = said)
        {
            for (int i = 0; i < texts.Count; i++)
            {
                each[i] = pack.Borrow(data, i);
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

    /// <summary>
    /// The records in <paramref name="directory"/> — null or empty for the
    /// directory <c>XMIP_AUDIT_DIRECTORY</c> names — that
    /// <paramref name="query"/> asks for, key then value in the header's words
    /// (<c>pattern</c>, <c>location</c>, <c>host</c>, <c>program</c>,
    /// <c>record</c>, <c>severity</c>, <c>action</c>, <c>from</c>,
    /// <c>to</c>, <c>sort</c>, <c>order</c>, <c>offset</c>, <c>limit</c>).
    /// </summary>
    /// <exception cref="ArgumentException">The capability refused the query,
    /// with its sentence.</exception>
    /// <exception cref="IOException">The file is there and could not be
    /// read, with why.</exception>
    public AuditRead Read(
        string? directory, IReadOnlyList<KeyValuePair<string, string>> query)
    {
        ArgumentNullException.ThrowIfNull(query);

        List<string> texts = [directory ?? string.Empty];

        foreach ((string key, string value) in query)
        {
            texts.Add(key);
            texts.Add(value);
        }

        Utf8Pack pack = new(texts);
        byte[] text = new byte[Room];
        nuint needed = 0;
        int status = Ask(pack, texts.Count, text, ref needed);

        if (needed > (nuint)text.Length)
        {
            text = new byte[(int)needed];
            status = Ask(pack, texts.Count, text, ref needed);
        }

        string said = Encoding.UTF8.GetString(text, 0, (int)Math.Min(needed, (nuint)text.Length));

        return status switch
        {
            0 => Answer(said),
            (int)XmipStatus.Invalid => throw new ArgumentException(said, nameof(query)),
            (int)XmipStatus.Io => throw new IOException(said),
            _ => throw new InvalidOperationException(
                $"the runtime answered an audit read with {(XmipStatus)status}"),
        };
    }

    private int Ask(Utf8Pack pack, int count, byte[] text, ref nuint needed)
    {
        XmipStr[] strings = new XmipStr[count];

        fixed (byte* data = pack.Bytes)
        fixed (XmipStr* each = strings)
        fixed (byte* written = text)
        fixed (nuint* length = &needed)
        {
            for (int i = 0; i < count; i++)
            {
                each[i] = pack.Borrow(data, i);
            }

            return _read(
                each[0], each + 1, (nuint)(count - 1), written, (nuint)text.Length, length);
        }
    }

    private static AuditRead Answer(string json)
    {
        using JsonDocument answer = JsonDocument.Parse(json);
        JsonElement root = answer.RootElement;

        return new AuditRead(
            root.GetProperty("file").GetString() ?? string.Empty,
            root.GetProperty("read").GetInt32(),
            root.GetProperty("matched").GetInt32(),
            root.GetProperty("offset").GetInt32(),
            root.GetProperty("limit").GetInt32(),
            [.. root.GetProperty("records").EnumerateArray().Select(Entry)],
            [.. root.GetProperty("groups").EnumerateArray().Select(Group)],
            Words(root, "actions"),
            Words(root, "columns"),
            Words(root, "severities"));
    }

    private static AuditEntry Entry(JsonElement record)
    {
        return new AuditEntry(
            Text(record, "audit_id") ?? string.Empty,
            Text(record, "at") ?? string.Empty,
            Text(record, "program") ?? string.Empty,
            Text(record, "host") ?? string.Empty,
            Text(record, "process") ?? string.Empty,
            Text(record, "location"),
            Text(record, "node"),
            Text(record, "cluster"),
            Text(record, "action") ?? string.Empty,
            Text(record, "phase") ?? string.Empty,
            Text(record, "severity") ?? string.Empty,
            Text(record, "message"),
            Text(record, "summary") ?? string.Empty,
            Texts(record.GetProperty("scope")),
            Texts(record.GetProperty("properties")),
            Flag(record, "hidden"));
    }

    private static AuditGroup Group(JsonElement group)
    {
        return new AuditGroup(
            Text(group, "kind") ?? string.Empty,
            Text(group, "who") ?? string.Empty,
            group.GetProperty("count").GetInt32(),
            group.GetProperty("warnings").GetInt32(),
            group.GetProperty("errors").GetInt32(),
            Text(group, "latest") ?? string.Empty,
            Flag(group, "hidden"));
    }

    private static bool Flag(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out JsonElement value)
            && value.ValueKind == JsonValueKind.True;
    }

    private static string[] Words(JsonElement root, string name)
    {
        return [.. root.GetProperty(name).EnumerateArray().Select(
            word => word.GetString() ?? string.Empty)];
    }

    private static string? Text(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out JsonElement value) ? value.GetString() : null;
    }

    private static Dictionary<string, string> Texts(JsonElement table)
    {
        return table.EnumerateObject().ToDictionary(
            pair => pair.Name, pair => pair.Value.GetString() ?? string.Empty,
            StringComparer.Ordinal);
    }
}
