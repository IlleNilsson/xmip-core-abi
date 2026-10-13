using System.Text;
using Xmip.Abi.Module;

namespace Xmip.Abi.Operate;

// Section 7's mood and counted rules: a mood's word and color, its parse, the
// worst-first order, the rollup, and a counted kind's word.
public sealed unsafe partial class RuntimeRules
{
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
        byte[] text =
            new byte[records.Sum(record => Encoding.UTF8.GetByteCount(record.Scope))];

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

    /// <summary>What a parent shows when <paramref name="worst"/> is the worst
    /// mood beneath it — <c>observe::Health::rolled</c>: Fine or Holding
    /// (ADR-0041).</summary>
    public HealthState Rolled(HealthState worst)
    {
        int rolled = 0;

        Check(_rolled((int)worst, &rolled));
        return (HealthState)rolled;
    }

    /// <summary>A counted kind as the word the estate uses —
    /// <c>observe::Counted::word</c>. Null for a value the runtime does not
    /// define.</summary>
    public string? CountedWord(Counted counted)
    {
        XmipStr text;

        return _countedWord((int)counted, &text) == 0 ? text.Read() : null;
    }
}
