using System.Text;
using Xmip.Abi.Module;

namespace Xmip.Abi.Operate;

/// <summary>
/// Several .NET strings encoded as UTF-8 end to end in one buffer, each
/// crossing as its own span of it: how a call that hands the runtime many
/// strings at once — an audit record, a subscription's filter, an Event to
/// publish — lays them out, written once.
/// </summary>
internal sealed unsafe class Utf8Pack
{
    private readonly int[] _starts;

    /// <summary>Encode <paramref name="texts"/>, in order.</summary>
    public Utf8Pack(IReadOnlyList<string> texts)
    {
        _starts = new int[texts.Count + 1];
        Bytes = new byte[texts.Sum(Encoding.UTF8.GetByteCount)];

        for (int i = 0; i < texts.Count; i++)
        {
            _starts[i + 1] = _starts[i] + Encoding.UTF8.GetBytes(texts[i], 0, texts[i].Length,
                Bytes, _starts[i]);
        }
    }

    /// <summary>How many strings are packed.</summary>
    public int Count => _starts.Length - 1;

    /// <summary>The buffer, to pin for the call.</summary>
    public byte[] Bytes { get; }

    /// <summary>The <paramref name="at"/>th string as a borrow of
    /// <paramref name="data"/>, which is <see cref="Bytes"/> pinned.</summary>
    public XmipStr Borrow(byte* data, int at)
    {
        return new XmipStr(data + _starts[at], (nuint)(_starts[at + 1] - _starts[at]));
    }
}
