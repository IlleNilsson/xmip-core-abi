using System.Globalization;

namespace Xmip.Surface;

// What is moving, as every surface writes it: a figure's flow, a rate per
// second and the rate's number.
public static partial class English
{
    /// <summary>What a figure is moving, as a reader's <see cref="FigureWatch"/>
    /// gave it: the rate, or — where the reader has seen one publication and so
    /// has no interval — that it is waiting, which is not a stall.</summary>
    public static string Flow(double? perSecond)
    {
        return perSecond is { } rate ? Rate(rate) : "waiting for the next round";
    }

    /// <summary>A rate per second as a person reads it: <see cref="Moving"/>
    /// and its unit.</summary>
    public static string Rate(double perSecond)
    {
        return $"{Moving(perSecond)}/s";
    }

    /// <summary>
    /// The number of a rate, without its unit, as every surface writes it:
    /// the K, M and G ladder, with one decimal below ten of a unit carried down
    /// to the unit itself — 0.3, 9.9, 240, 1.2K. A trickle is written 0.3,
    /// below a twentieth with two decimals, 0.03, and below a two-hundredth
    /// <c>&lt;0.01</c> — never 0, because <c>0</c> means stalled and nothing
    /// else may. One Message in a publisher's half-minute window is 0.03 (the
    /// Topology, ADR-0052, amendment 2026-11-10); until 2026-11-11 it was
    /// written 0. The prompt writes this bare, since a prompt's units are
    /// learned once (the owner, 2026-09-20: *the Xmip Prompt does not need /s
    /// spelled out*); everything else writes <see cref="Rate"/>.
    /// </summary>
    public static string Moving(double perSecond)
    {
        if (perSecond is > 0D and < 0.005D)
        {
            return "<0.01";
        }

        string[] units = ["K", "M", "G"];
        double value = perSecond;
        int unit = -1;

        while (unit < units.Length - 1 && value >= 999.5)
        {
            value /= 1000;
            unit++;
        }

        string format = value switch
        {
            < 0.05D => "0.##",
            < 9.95D => "0.#",
            _ => "0",
        };

        return value.ToString(format, CultureInfo.InvariantCulture)
            + (unit < 0 ? string.Empty : units[unit]);
    }
}
