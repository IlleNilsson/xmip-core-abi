using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Xmip.Abi.Module;

namespace Xmip.Abi.Operate;

/// <summary>
/// Section 7 of <c>include/xmip_operate.h</c>, crossed by P/Invoke: the rules
/// a surface calls instead of keeping its own. Scope containment and a
/// scope's parts are <c>observe::Scope</c>'s, the stage words and their parse
/// are <c>node::Stage</c>'s, and a mood's word, its color name and the
/// worst-first order are <c>observe::Health</c>'s and <c>observe::Standing</c>'s;
/// the runtime's library forwards each, and this binds each once for every
/// .NET surface (ADR-0052 and ADR-0027, amendments 2026-09-24).
/// </summary>
/// <remarks>
/// Pure calls: none reads the snapshot or holds a table, so one instance
/// serves a whole process from any thread and is never released. What it
/// answers is copied out at once; nothing borrowed outlives the call.
/// </remarks>
// Every call encodes its strings into stack buffers the encoder then fills;
// zeroing them first is wasted on a path a board takes thousands of times.
[SkipLocalsInit]
public sealed unsafe class RuntimeRules
{
    // A scope or a word is short. Up to this many bytes it is encoded on the
    // stack, because a board compares thousands of records at a time.
    private const int Small = 512;

    private readonly delegate* unmanaged[Cdecl]<XmipStr, XmipStr, byte*, int> _contains;
    private readonly delegate* unmanaged[Cdecl]<XmipStr, XmipStr*, nuint, nuint*, int> _parts;
    private readonly delegate* unmanaged[Cdecl]<
        XmipStr, XmipStr*, nuint, nuint*, byte*, nuint, nuint*, int> _declared;
    private readonly delegate* unmanaged[Cdecl]<int, XmipStr*, int> _word;
    private readonly delegate* unmanaged[Cdecl]<int, XmipStr*, int> _color;
    private readonly delegate* unmanaged[Cdecl]<XmipStr, int*, int> _named;
    private readonly delegate* unmanaged[Cdecl]<XmipHealthEntry*, nuint, nuint*, int> _order;

    private RuntimeRules(nint library, string path)
    {
        Source = path;
        _contains = (delegate* unmanaged[Cdecl]<XmipStr, XmipStr, byte*, int>)
            Export(library, OperateAbi.ScopeContainsEntrypoint);
        _parts = (delegate* unmanaged[Cdecl]<XmipStr, XmipStr*, nuint, nuint*, int>)
            Export(library, OperateAbi.ScopePartsEntrypoint);
        _declared = (delegate* unmanaged[Cdecl]<
            XmipStr, XmipStr*, nuint, nuint*, byte*, nuint, nuint*, int>)
            Export(library, OperateAbi.StageDeclaredEntrypoint);
        _word = (delegate* unmanaged[Cdecl]<int, XmipStr*, int>)
            Export(library, OperateAbi.HealthWordEntrypoint);
        _color = (delegate* unmanaged[Cdecl]<int, XmipStr*, int>)
            Export(library, OperateAbi.HealthColorEntrypoint);
        _named = (delegate* unmanaged[Cdecl]<XmipStr, int*, int>)
            Export(library, OperateAbi.HealthNamedEntrypoint);
        _order = (delegate* unmanaged[Cdecl]<XmipHealthEntry*, nuint, nuint*, int>)
            Export(library, OperateAbi.HealthOrderEntrypoint);

        delegate* unmanaged[Cdecl]<XmipStr*, nuint, nuint*, int> words =
            (delegate* unmanaged[Cdecl]<XmipStr*, nuint, nuint*, int>)
            Export(library, OperateAbi.StageWordsEntrypoint);
        StageWords = Words(words);
    }

    /// <summary>The library these rules were loaded from.</summary>
    public string Source { get; }

    /// <summary>The words a node may declare, in message-path order —
    /// <c>node::Stage::WORDS</c>, read once when the library loads.</summary>
    public IReadOnlyList<string> StageWords { get; }

    /// <summary>Section 7's symbols, each of which a runtime must export.</summary>
    public static IReadOnlyList<string> Entrypoints { get; } =
    [
        OperateAbi.ScopeContainsEntrypoint,
        OperateAbi.ScopePartsEntrypoint,
        OperateAbi.StageWordsEntrypoint,
        OperateAbi.StageDeclaredEntrypoint,
        OperateAbi.HealthWordEntrypoint,
        OperateAbi.HealthColorEntrypoint,
        OperateAbi.HealthNamedEntrypoint,
        OperateAbi.HealthOrderEntrypoint,
    ];

    /// <summary>Load the runtime's library and bind section 7. Null, with the
    /// reason, when there is none, it does not load, or it lacks one of
    /// <see cref="Entrypoints"/>.</summary>
    public static RuntimeRules? Load(string path, out string reason)
    {
        if (!LibraryCopy.TryLoad(path, out nint library, out reason))
        {
            return null;
        }

        foreach (string entrypoint in Entrypoints)
        {
            if (!NativeLibrary.TryGetExport(library, entrypoint, out _))
            {
                NativeLibrary.Free(library);
                reason = $"{path} does not export {entrypoint}";
                return null;
            }
        }

        return new RuntimeRules(library, path);
    }

    /// <summary>Whether <paramref name="candidate"/> is <paramref name="scope"/>
    /// or sits beneath it — <c>observe::Scope::contains</c>.</summary>
    public bool Contains(string scope, string candidate)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(candidate);

        int scopeMax = Encoding.UTF8.GetMaxByteCount(scope.Length);
        int candidateMax = Encoding.UTF8.GetMaxByteCount(candidate.Length);
        Span<byte> scopeBytes = scopeMax <= Small ? stackalloc byte[Small] : new byte[scopeMax];
        Span<byte> candidateBytes = candidateMax <= Small
            ? stackalloc byte[Small]
            : new byte[candidateMax];
        int scopeLength = Encoding.UTF8.GetBytes(scope, scopeBytes);
        int candidateLength = Encoding.UTF8.GetBytes(candidate, candidateBytes);
        byte contains = 0;

        fixed (byte* scopeData = scopeBytes)
        fixed (byte* candidateData = candidateBytes)
        {
            Check(_contains(
                new XmipStr(scopeData, (nuint)scopeLength),
                new XmipStr(candidateData, (nuint)candidateLength),
                &contains));
        }

        return contains != 0;
    }

    /// <summary>The segments of a scope's path, top first —
    /// <c>observe::Scope::segments</c>. The root has none.</summary>
    public string[] Parts(string scope)
    {
        ArgumentNullException.ThrowIfNull(scope);

        int max = Encoding.UTF8.GetMaxByteCount(scope.Length);
        Span<byte> bytes = max <= Small ? stackalloc byte[Small] : new byte[max];
        int length = Encoding.UTF8.GetBytes(scope, bytes);

        fixed (byte* data = bytes)
        {
            XmipStr text = new(data, (nuint)length);
            const int Deep = 16;
            XmipStr* first = stackalloc XmipStr[Deep];
            nuint count = 0;

            Check(_parts(text, first, Deep, &count));

            // Each part borrows from `bytes`, still pinned here.
            if (count <= Deep)
            {
                string[] parts = new string[count];

                for (int i = 0; i < parts.Length; i++)
                {
                    parts[i] = first[i].Read();
                }

                return parts;
            }

            XmipStr[] found = new XmipStr[count];

            fixed (XmipStr* into = found)
            {
                Check(_parts(text, into, count, &count));
            }

            return [.. found.Select(part => part.Read())];
        }
    }

    /// <summary>
    /// The stages a declaration names, in message-path order and each once —
    /// <c>node::Stage::declared</c>. A declaration naming any other word
    /// declares no stage, and <paramref name="refusal"/> is the REFUSED
    /// sentence naming it (ADR-0055); otherwise it is empty.
    /// </summary>
    public IReadOnlyList<string> Declared(string declared, out string refusal)
    {
        ArgumentNullException.ThrowIfNull(declared);

        byte[] bytes = Encoding.UTF8.GetBytes(declared);
        XmipStr[] stages = new XmipStr[StageWords.Count];
        byte[] said = [];
        nuint count = 0;
        nuint saidLength = Small;
        int status;

        // Asked again only when the refusal did not fit the first buffer.
        do
        {
            said = new byte[saidLength];

            fixed (byte* data = bytes)
            fixed (XmipStr* into = stages)
            fixed (byte* sentence = said)
            {
                status = _declared(
                    new XmipStr(data, (nuint)bytes.Length), into, (nuint)stages.Length, &count,
                    sentence, (nuint)said.Length, &saidLength);
            }
        }
        while (saidLength > (nuint)said.Length);

        if (status == (int)XmipStatus.Invalid)
        {
            refusal = Encoding.UTF8.GetString(said, 0, checked((int)saidLength));
            return [];
        }

        Check(status);
        refusal = string.Empty;

        // Static words: they outlive the call.
        return [.. stages.Take(checked((int)count)).Select(stage => stage.Read())];
    }

    /// <summary>The mood as the word the estate uses, lower case —
    /// <c>observe::Health::word</c>. Null for a value the runtime does not
    /// define.</summary>
    public string? Word(HealthState state)
    {
        XmipStr text;

        return _word((int)state, &text) == 0 ? text.Read() : null;
    }

    /// <summary>The name of the color a surface paints a mood in (ADR-0041) —
    /// <c>observe::Health::color</c>. Null for a value the runtime does not
    /// define.</summary>
    public string? Color(HealthState state)
    {
        XmipStr text;

        return _color((int)state, &text) == 0 ? text.Read() : null;
    }

    /// <summary>The mood a word names, exactly — <c>observe::Health::named</c>.
    /// Null when it names none.</summary>
    public HealthState? Named(string word)
    {
        ArgumentNullException.ThrowIfNull(word);

        int max = Encoding.UTF8.GetMaxByteCount(word.Length);
        Span<byte> bytes = max <= Small ? stackalloc byte[Small] : new byte[max];
        int length = Encoding.UTF8.GetBytes(word, bytes);
        int state = 0;
        int status;

        fixed (byte* data = bytes)
        {
            status = _named(new XmipStr(data, (nuint)length), &state);
        }

        return status == 0 ? (HealthState)state : null;
    }

    /// <summary>
    /// Many records in the worst-first order, in one call —
    /// <c>observe::Standing::worst_first</c>: the positions the records hold
    /// in <paramref name="records"/>, the worst record's first, equals in the
    /// order they came. What a surface orders a whole publication by.
    /// </summary>
    public int[] WorstFirst(IReadOnlyList<HealthRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);

        if (records.Count == 0)
        {
            return [];
        }

        // Every scope encoded once, end to end in one buffer, each entry
        // borrowing its own span of it for the one call.
        int[] starts = new int[records.Count + 1];
        byte[] text = new byte[records.Sum(record => Encoding.UTF8.GetByteCount(record.Scope))];

        for (int at = 0; at < records.Count; at++)
        {
            starts[at + 1] = starts[at]
                + Encoding.UTF8.GetBytes(records[at].Scope, text.AsSpan(starts[at]));
        }

        XmipHealthEntry[] entries = new XmipHealthEntry[records.Count];
        nuint[] order = new nuint[records.Count];

        fixed (byte* scopes = text)
        fixed (XmipHealthEntry* into = entries)
        fixed (nuint* positions = order)
        {
            for (int at = 0; at < entries.Length; at++)
            {
                int length = starts[at + 1] - starts[at];

                into[at].Scope = new XmipStr(scopes + starts[at], (nuint)length);
                into[at].Health = (int)records[at].State;
                into[at].Severity = records[at].Severity;
            }

            Check(_order(into, (nuint)entries.Length, positions));
        }

        return [.. order.Select(position => checked((int)position))];
    }

    private static nint Export(nint library, string entrypoint)
    {
        return NativeLibrary.GetExport(library, entrypoint);
    }

    private static string[] Words(delegate* unmanaged[Cdecl]<XmipStr*, nuint, nuint*, int> words)
    {
        nuint count = 0;

        Check(words(null, 0, &count));

        XmipStr[] found = new XmipStr[count];

        fixed (XmipStr* into = found)
        {
            Check(words(into, count, &count));
        }

        return [.. found.Select(word => word.Read())];
    }

    // Any other status is a runtime breaking section 7's promise, not an answer.
    private static void Check(int status)
    {
        if (status != 0)
        {
            throw new InvalidOperationException(
                $"the runtime answered a section 7 rule with {(XmipStatus)status}");
        }
    }
}
