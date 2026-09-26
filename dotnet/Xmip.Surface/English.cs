using System.Globalization;
using Xmip.Abi.Module;
using Xmip.Abi.Operate;

namespace Xmip.Surface;

/// <summary>
/// How a surface says things to a person, once (ADR-0052 clause 1): a mood as
/// a word, a rollup, an age, a count, and what the runtime answered when it
/// was asked to start, validate, pause or resume. A mood's word and color
/// name are the runtime's (<c>observe::Health</c>), called here and written
/// nowhere in .NET. A status crosses the
/// boundary as a code; this is the one place it becomes a sentence, so the
/// board, the command and the cmdlet say the same thing about the same code.
/// </summary>
public static class English
{
    /// <summary>The mood as the word the estate uses, lower case —
    /// <c>observe::Health::word</c>, called in the runtime, so the word a
    /// snapshot publishes and the word a surface prints are one word.
    /// <c>unknown</c> for a value the runtime does not define.</summary>
    public static string Mood(HealthState state)
    {
        return RuntimeLibrary.Rules.Word(state) ?? "unknown";
    }

    /// <summary>A counted kind as the word the estate uses, lower case —
    /// <c>observe::Counted::word</c>, called in the runtime, so the word a
    /// publication writes and the word a command names a figure by are one
    /// word. <c>unknown</c> for a value the runtime does not define.</summary>
    public static string Kind(Counted counted)
    {
        return RuntimeLibrary.Rules.CountedWord(counted) ?? "unknown";
    }

    /// <summary>What a person reads a topology kind as — <c>virtual
    /// machine</c> — <c>observe::NodeKind::name</c>, called in the runtime
    /// (ADR-0052, amendment 2026-09-25: the web GUI kept its own until then).
    /// <c>unknown</c> for a value the runtime does not define.</summary>
    public static string Name(TopologyNodeKind kind)
    {
        return RuntimeLibrary.Rules.Publications.Words(kind)?.Name ?? "unknown";
    }

    /// <summary>What a person reads an origin as — <c>configured and
    /// observed</c> — <c>observe::Origin::name</c>, called in the
    /// runtime.</summary>
    public static string Name(TopologyOrigin origin)
    {
        return RuntimeLibrary.Rules.Publications.Words(origin)?.Name ?? "unknown";
    }

    /// <summary>What a person reads a communication pattern as — <c>Publish →
    /// consume</c> — <c>observe::Pattern::name</c>, called in the
    /// runtime.</summary>
    public static string Name(CommunicationPattern pattern)
    {
        return RuntimeLibrary.Rules.Publications.Words(pattern)?.Name ?? "unknown";
    }

    /// <summary>The word a publication writes a topology kind as —
    /// <c>virtual-machine</c> — <c>observe::NodeKind::word</c>, for a surface
    /// that styles by it. <c>unknown</c> for a value the runtime does not
    /// define.</summary>
    public static string Word(TopologyNodeKind kind)
    {
        return RuntimeLibrary.Rules.Publications.Words(kind)?.Word ?? "unknown";
    }

    /// <summary>The word a publication writes an origin as —
    /// <c>observe::Origin::word</c>.</summary>
    public static string Word(TopologyOrigin origin)
    {
        return RuntimeLibrary.Rules.Publications.Words(origin)?.Word ?? "unknown";
    }

    /// <summary>The word a publication writes a communication pattern as —
    /// <c>publish-consume</c> — <c>observe::Pattern::word</c>.</summary>
    public static string Word(CommunicationPattern pattern)
    {
        return RuntimeLibrary.Rules.Publications.Words(pattern)?.Word ?? "unknown";
    }

    /// <summary>A value as a person reads it, or an em dash where the
    /// publisher said none.</summary>
    public static string Value(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "—" : value;
    }

    /// <summary>A fraction of one as a whole percentage, clamped to its
    /// range.</summary>
    public static string Percent(double value)
    {
        return string.Create(
            CultureInfo.InvariantCulture, $"{Math.Clamp(value, 0D, 1D):P0}");
    }

    /// <summary>The mood a word names, or null when it names none —
    /// <c>observe::Health::named</c>, for a surface reading a published
    /// snapshot.</summary>
    public static HealthState? MoodOf(string? word)
    {
        return word is null ? null : RuntimeLibrary.Rules.Named(word);
    }

    /// <summary>
    /// The color a surface paints a mood in, by name (ADR-0041) —
    /// <c>observe::Health::color</c>, called in the runtime. The name is the
    /// estate's — the stylesheet's tokens carry the same names, and a console
    /// picks its nearest color from the word — so the board and the prompt
    /// cannot paint one mood two ways (ADR-0052 clause 1). A mood the runtime
    /// does not define is muted.
    /// </summary>
    public static string Color(HealthState state)
    {
        return RuntimeLibrary.Rules.Color(state) ?? "muted";
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

    /// <summary>A figure as a person reads it: thousands separated, and a
    /// dash — never a zero — when the publisher has not published it
    /// (ADR-0052, amendment 2026-09-14). The one spelling the board, the
    /// topology inspector and <c>xmip-cli</c> give a count.</summary>
    public static string Figure(ulong? value)
    {
        return value?.ToString("N0", CultureInfo.InvariantCulture) ?? "–";
    }

    /// <summary>The six figures on one line, in the order every surface says
    /// them, an unpublished one a dash: what <c>xmip-cli</c> prints beside a
    /// row and the web's drill beside a branch.</summary>
    public static string Figures(Figures figures)
    {
        ArgumentNullException.ThrowIfNull(figures);

        return $"Streams {Figure(figures.Streams)}  " +
            $"Messages {Figure(figures.Messages)}  " +
            $"Journeys {Figure(figures.Journeys)}  " +
            $"Bytes {Figure(figures.Bytes)}  " +
            $"Retrying {Figure(figures.Retrying)}  " +
            $"Failed {Figure(figures.Failed)}";
    }

    /// <summary>What a surface says of a scope it holds nothing at, naming
    /// where it looked: the command line and the cmdlet say the same.</summary>
    public static string NothingAt(string scope, string source)
    {
        return $"Nothing at {scope} ({source}).";
    }

    /// <summary>How a count moved since the board last saw it change: the
    /// increase, and the rate it implies over the time it took. Nothing yet
    /// when the board has seen only one value.</summary>
    public static string Flow(ulong delta, TimeSpan over)
    {
        return over <= TimeSpan.Zero
            ? "waiting for the next round"
            : $"+{delta.ToString("N0", CultureInfo.InvariantCulture)} last round · " +
                Rate(delta / over.TotalSeconds);
    }

    /// <summary>A rate per second as a person reads it: whole from ten up,
    /// one decimal below, so a trickle is never rounded to a stall.</summary>
    public static string Rate(double perSecond)
    {
        string rate = perSecond >= 10
            ? perSecond.ToString("N0", CultureInfo.InvariantCulture)
            : perSecond.ToString("N1", CultureInfo.InvariantCulture);

        return $"{rate}/s";
    }

    /// <summary>What passes over a communication link, as the topology says
    /// it on the line itself (ADR-0052, amendment 2026-09-25): the volume the
    /// publisher counted and the rate it stated, or — for a link configured
    /// and never used — that nothing has passed, so a path with no traffic is
    /// seen as such rather than as a zero.</summary>
    public static string Traffic(CommunicationLink link)
    {
        ArgumentNullException.ThrowIfNull(link);

        return link.Origin == TopologyOrigin.Configured && link.Volume == 0
            ? "configured · no traffic observed"
            : $"{Figure(link.Volume)} · {Rate(link.Rate)}";
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
