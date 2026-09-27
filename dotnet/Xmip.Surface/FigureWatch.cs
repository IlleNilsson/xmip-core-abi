namespace Xmip.Surface;

/// <summary>
/// What one reader has seen a scope's figures do: the last publication it
/// read and when, so the next has an interval to divide by, and the rate it
/// gave, so a surface can say whether the rate is climbing. The one place a
/// face remembers figures between two reads; <see cref="FigureFlow.Between"/>
/// is the one calculation. Until 2026-09-27 the prompt kept its own last value
/// and clock and the board another, with a delta of its own.
/// </summary>
/// <remarks>
/// A read of the same publication again — the board redraws when the operator
/// drills, not only when the publisher publishes — is no interval: the flow is
/// the one that publication gave. The publication is told by the change feed's
/// <see cref="ScopeIndex.Revision"/>; a surface with no feed numbers every
/// publication 0, and then every read is taken as a new one, which is what a
/// reader that reads only on a notice needs. The clock is the reader's: a
/// published snapshot carries no observation time for its counts.
/// </remarks>
public sealed class FigureWatch
{
    private Figures? seen;
    private DateTimeOffset seenAt;
    private ulong revision;
    private FigureFlow? known;

    /// <summary>What the last publication read gave:
    /// <see cref="FigureFlow.Unknown"/> before there were two.</summary>
    public FigureFlow Flow { get; private set; } = FigureFlow.Unknown;

    /// <summary>The last known rate before <see cref="Flow"/>, which says
    /// whether the rate is climbing or falling; null before there was one. A
    /// publication that gave no rate leaves the one before in place.</summary>
    public FigureFlow? Before { get; private set; }

    /// <summary>Read one publication's figures, observed at
    /// <paramref name="at"/>, and answer what they are moving.</summary>
    public FigureFlow See(Figures now, DateTimeOffset at, ulong publication = 0)
    {
        ArgumentNullException.ThrowIfNull(now);

        if (seen is not null && publication != 0 && publication == revision)
        {
            return Flow;
        }

        FigureFlow flow = FigureFlow.Between(
            now, seen, seen is null ? TimeSpan.Zero : at - seenAt);

        Before = known;
        Flow = flow;
        known = flow.Known ? flow : known;
        seen = now;
        seenAt = at;
        revision = publication;

        return flow;
    }
}
