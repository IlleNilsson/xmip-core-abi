using System.Globalization;
using Xmip.Abi.Module;
using Xmip.Abi.Operate;

namespace Xmip.Surface;

/// <summary>
/// How a surface says things to a person, once (ADR-0052 clause 1): a mood as
/// a word, a rollup, an age, a count, and what the runtime answered when it
/// was asked to start, validate, pause or resume. A status crosses the
/// boundary as a code; this is the one place it becomes a sentence, so the
/// board, the command and the cmdlet say the same thing about the same code.
/// </summary>
public static class English
{
    /// <summary>The mood as the word the estate uses — the playground's and
    /// the header's, lower case.</summary>
    public static string Mood(HealthState state)
    {
        return state switch
        {
            HealthState.Fine => "fine",
            HealthState.Paused => "paused",
            HealthState.Working => "working",
            HealthState.Stressed => "stressed",
            HealthState.Exhausted => "exhausted",
            HealthState.Done => "done",
            HealthState.Holding => "holding",
            _ => "unknown",
        };
    }

    /// <summary>The rollup over a set of leaves as a word: <c>fine</c>,
    /// <c>holding</c>, or <c>nothing recorded</c> when there are none.</summary>
    public static string Rollup(IEnumerable<HealthRecord> records)
    {
        HealthState? rolled = ScopeTree.Rollup(records);

        return rolled is null ? "nothing recorded" : Mood(rolled.Value);
    }

    /// <summary>How long ago something was observed, as a person reads it.</summary>
    public static string Age(DateTimeOffset observed)
    {
        return Age(observed, DateTimeOffset.UtcNow);
    }

    /// <summary>How long before <paramref name="now"/> something was observed.</summary>
    public static string Age(DateTimeOffset observed, DateTimeOffset now)
    {
        TimeSpan age = now - observed;

        if (age < TimeSpan.Zero)
        {
            age = TimeSpan.Zero;
        }

        return age.TotalSeconds < 60
            ? $"{(int)age.TotalSeconds}s ago"
            : age.TotalMinutes < 60
                ? $"{(int)age.TotalMinutes}m ago"
                : $"{(int)age.TotalHours}h ago";
    }

    /// <summary>A measurement as a figure: bytes scaled, everything else with
    /// thousands separated, a dash when there is none.</summary>
    public static string Count(MeasurementRecord? measured)
    {
        return measured switch
        {
            null => "–",
            { Counted: Counted.Bytes } bytes => Bytes(bytes.Value),
            { } count => count.Value.ToString("N0", CultureInfo.InvariantCulture),
        };
    }

    /// <summary>How a count moved since the board last saw it change: the
    /// increase, and the rate it implies over the time it took. Nothing yet
    /// when the board has seen only one value.</summary>
    public static string Flow(ulong delta, TimeSpan over)
    {
        if (over <= TimeSpan.Zero)
        {
            return "waiting for the next round";
        }

        double perSecond = delta / over.TotalSeconds;
        string rate = perSecond >= 10
            ? perSecond.ToString("N0", CultureInfo.InvariantCulture)
            : perSecond.ToString("N1", CultureInfo.InvariantCulture);

        return $"+{delta.ToString("N0", CultureInfo.InvariantCulture)} last round · {rate}/s";
    }

    /// <summary>A byte count scaled to the unit a person reads.</summary>
    public static string Bytes(ulong value)
    {
        return value switch
        {
            >= 1_000_000_000 => Scaled(value / 1_000_000_000.0, "GB"),
            >= 1_000_000 => Scaled(value / 1_000_000.0, "MB"),
            >= 1_000 => Scaled(value / 1_000.0, "kB"),
            _ => $"{value} B",
        };
    }

    /// <summary>What the runtime said when asked to start a node from a saved
    /// configuration.</summary>
    public static string Started(string configurationPath, XmipStatus status)
    {
        return status switch
        {
            XmipStatus.Ok => $"started {configurationPath}",
            XmipStatus.Unsupported =>
                $"this runtime does not export {OperateAbi.StartEntrypoint}",
            _ => $"{configurationPath} refused: {status.Explain()}; the health tree says why",
        };
    }

    /// <summary>What the runtime said when asked to validate a configuration
    /// document without starting it.</summary>
    public static string Validated(string configurationPath, ValidationRecord answer)
    {
        if (answer.IsValid)
        {
            return $"{configurationPath} is valid";
        }

        if (answer.Status == XmipStatus.Unsupported)
        {
            return $"this runtime does not export {OperateAbi.ValidateEntrypoint}";
        }

        string why = answer.Problems.Count == 0
            ? answer.Status.Explain()
            : string.Join("; ", answer.Problems);

        return $"{configurationPath} is invalid: {why}";
    }

    /// <summary>What the runtime said when asked to pause a scope.</summary>
    public static string Paused(string scope, XmipStatus status)
    {
        return status == XmipStatus.Ok
            ? $"paused {scope}"
            : $"nothing to pause at {scope} ({status.Explain()})";
    }

    /// <summary>What the runtime said when asked to resume a scope.</summary>
    public static string Resumed(string scope, XmipStatus status)
    {
        return status == XmipStatus.Ok
            ? $"resumed {scope}"
            : $"nothing to resume at {scope} ({status.Explain()})";
    }

    private static string Scaled(double value, string unit)
    {
        return string.Create(CultureInfo.InvariantCulture, $"{value:F1} {unit}");
    }
}
