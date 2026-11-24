using System.Globalization;
using System.Text;
using Xmip.Abi.Module;

namespace Xmip.Module;

/// <summary>
/// Where a <see cref="Promotion"/> hands each value it promotes: the node's
/// sink (<c>xmip_module.h</c> section 14), good for one call to
/// <see cref="Promotion.Promote"/> alone.
/// </summary>
public sealed class Promoted
{
    private XmipPromotionSink* _sink;

    internal Promoted(XmipPromotionSink* sink)
    {
        _sink = sink;
    }

    /// <summary>Text, under <paramref name="property"/>.</summary>
    public void Text(string property, string value)
    {
        Hand(property, XmipValueKind.String, Encoding.UTF8.GetBytes(value));
    }

    /// <summary>A whole number, under <paramref name="property"/>.</summary>
    public void Number(string property, long value)
    {
        Hand(property, XmipValueKind.Number, Written(value.ToString(CultureInfo.InvariantCulture)));
    }

    /// <summary>A number, under <paramref name="property"/>.</summary>
    public void Number(string property, double value)
    {
        string written = value.ToString("R", CultureInfo.InvariantCulture);

        Hand(property, XmipValueKind.Number, Written(written));
    }

    /// <summary>A boolean, under <paramref name="property"/>.</summary>
    public void Boolean(string property, bool value)
    {
        Hand(property, XmipValueKind.Bool, Written(value ? "true" : "false"));
    }

    /// <summary>Bytes, under <paramref name="property"/>.</summary>
    public void Binary(string property, ReadOnlySpan<byte> value)
    {
        Hand(property, XmipValueKind.Binary, value);
    }

    /// <summary>Good for no further value: the call it was handed to has
    /// returned.</summary>
    internal void Close()
    {
        _sink = null;
    }

    private static byte[] Written(string text)
    {
        return Encoding.ASCII.GetBytes(text);
    }

    private void Hand(string property, XmipValueKind kind, ReadOnlySpan<byte> value)
    {
        ArgumentNullException.ThrowIfNull(property);

        XmipPromotionSink* sink = _sink;

        if (sink is null)
        {
            throw new InvalidOperationException(
                "a value is handed during the Promote call it was given for, never after");
        }

        byte[] name = Encoding.UTF8.GetBytes(property);
        int answer;

        unsafe
        {
            fixed (byte* named = name)
            fixed (byte* bytes = value)
            {
                answer = sink->Promoted(
                    sink->Ctx,
                    new XmipStr(named, (nuint)name.Length),
                    kind,
                    new XmipSlice(bytes, (nuint)value.Length));
            }
        }

        if (answer != (int)XmipStatus.Ok)
        {
            throw new BoundaryException(answer, $"the node refused the value of '{property}'");
        }
    }
}
