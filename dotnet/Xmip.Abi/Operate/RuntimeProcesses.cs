using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Xmip.Abi.Module;

namespace Xmip.Abi.Operate;

/// <summary>
/// Section 13 of <c>include/xmip_operate.h</c>, crossed by P/Invoke: a System
/// Process declared, and the declarations that stand (ADR-0053 clause 3). The
/// file, its directory, its words and its reading are <c>xmip-core-node</c>'s;
/// this binds the two calls once for every .NET program and for the estate's
/// PowerShell module, which write and read no declaration of their own.
/// </summary>
/// <remarks>
/// Pure in the header's sense: it reads no snapshot and holds nothing, so one
/// instance serves a whole process from any thread. The list is the header's
/// JSON, in memory only (ADR-0031 clause 2), read here into
/// <see cref="ProcessStanding"/>.
/// </remarks>
public sealed unsafe class RuntimeProcesses
{
    // A file's path, or a refusal, fits in this; the list is asked for again
    // at its true length.
    private const int Room = 16 * 1024;

    private readonly delegate* unmanaged[Cdecl]<
        XmipStr, XmipStr, XmipStr, XmipStr*, nuint, byte*, nuint, nuint*, int> _declare;

    private readonly delegate* unmanaged[Cdecl]<XmipStr, byte*, nuint, nuint*, int> _list;

    internal RuntimeProcesses(nint library)
    {
        _declare = (delegate* unmanaged[Cdecl]<
            XmipStr, XmipStr, XmipStr, XmipStr*, nuint, byte*, nuint, nuint*, int>)
            NativeLibrary.GetExport(library, OperateAbi.ProcessDeclareEntrypoint);
        _list = (delegate* unmanaged[Cdecl]<XmipStr, byte*, nuint, nuint*, int>)
            NativeLibrary.GetExport(library, OperateAbi.ProcessDeclarationsEntrypoint);
    }

    /// <summary>Section 13's symbols, which a runtime must export.</summary>
    public static IReadOnlyList<string> Entrypoints { get; } =
        [OperateAbi.ProcessDeclareEntrypoint, OperateAbi.ProcessDeclarationsEntrypoint];

    /// <summary>
    /// Declare the calling process where the node says: its
    /// <paramref name="name"/>, <paramref name="location"/> and
    /// <paramref name="purpose"/> (<c>test</c> or <c>runtime</c>), and what
    /// else it says, key then value. <see cref="XmipStatus.Ok"/> with the file
    /// the declaration stands in, which the caller removes where the process
    /// ends; <see cref="XmipStatus.Invalid"/> with the node's refusal, or
    /// <see cref="XmipStatus.Io"/> with why the file could not be written, in
    /// <paramref name="answer"/>.
    /// </summary>
    public XmipStatus Declare(
        string name,
        string location,
        string purpose,
        IReadOnlyList<KeyValuePair<string, string>>? said,
        out string answer)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(location);
        ArgumentNullException.ThrowIfNull(purpose);

        List<string> texts = [name, location, purpose];

        foreach ((string key, string value) in said ?? [])
        {
            texts.Add(key);
            texts.Add(value);
        }

        Utf8Pack pack = new(texts);
        XmipStr[] strings = new XmipStr[texts.Count];
        byte[] text = new byte[Room];
        nuint length = 0;
        int status;

        fixed (byte* data = pack.Bytes)
        fixed (XmipStr* each = strings)
        fixed (byte* written = text)
        {
            for (int i = 0; i < texts.Count; i++)
            {
                each[i] = pack.Borrow(data, i);
            }

            status = _declare(each[0], each[1], each[2], each + 3, (nuint)(texts.Count - 3),
                written, (nuint)text.Length, &length);
        }

        answer = Encoding.UTF8.GetString(text, 0, (int)Math.Min(length, (nuint)text.Length));

        return status switch
        {
            0 or (int)XmipStatus.Invalid or (int)XmipStatus.Io => (XmipStatus)status,
            _ => throw new InvalidOperationException(
                $"the runtime answered a process declaration with {(XmipStatus)status}"),
        };
    }

    /// <summary>
    /// Every declaration standing in <paramref name="directory"/> — null or
    /// empty for the directory the node names — whether or not its process
    /// still runs, which is the caller's to judge.
    /// </summary>
    public ProcessDeclarations Read(string? directory)
    {
        byte[] place = Encoding.UTF8.GetBytes(directory ?? string.Empty);
        byte[] text = new byte[Room];
        nuint needed = 0;
        int status = List(place, text, ref needed);

        if (needed > (nuint)text.Length)
        {
            text = new byte[(int)needed];
            status = List(place, text, ref needed);
        }

        if (status != 0)
        {
            throw new InvalidOperationException(
                $"the runtime answered the process declarations with {(XmipStatus)status}");
        }

        using JsonDocument answer = JsonDocument.Parse(text.AsMemory(0, (int)needed));
        JsonElement root = answer.RootElement;

        return new ProcessDeclarations(
            root.GetProperty("directory").GetString() ?? string.Empty,
            [.. root.GetProperty("processes").EnumerateArray().Select(Standing)]);
    }

    private static ProcessStanding Standing(JsonElement process)
    {
        return new ProcessStanding(
            process.GetProperty("file").GetString() ?? string.Empty,
            process.GetProperty("name").GetString() ?? string.Empty,
            process.GetProperty("location").GetString() ?? string.Empty,
            process.GetProperty("purpose").GetString() ?? string.Empty,
            process.GetProperty("pid").GetInt32(),
            process.GetProperty("started_unix").GetInt64(),
            process.GetProperty("path").GetString() ?? string.Empty,
            process.GetProperty("said").EnumerateObject().ToDictionary(
                said => said.Name,
                said => said.Value.GetString() ?? string.Empty,
                StringComparer.Ordinal));
    }

    private int List(byte[] place, byte[] text, ref nuint needed)
    {
        fixed (byte* placeData = place)
        fixed (byte* textData = text)
        fixed (nuint* length = &needed)
        {
            return _list(
                new XmipStr(placeData, (nuint)place.Length), textData, (nuint)text.Length, length);
        }
    }
}
