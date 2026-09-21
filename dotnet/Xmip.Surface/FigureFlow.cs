namespace Xmip.Surface;

/// <summary>
/// What a scope is moving now: five figures as a rate per second, between two
/// publications a reader saw. The owner, 2026-09-20: *the
/// CLI/PowerShell status number does not mean anything over time. There has to
/// be a logical cap on summarising the R, P, S, T and F numbers.* A total is
/// bounded by uptime and says nothing an operator can act on; a rate is bounded
/// by throughput, and it can say <c>0/s</c> — stalled — which a rising total
/// can never say.
/// </summary>
/// <remarks>
/// Here rather than in a face, so that every surface reads one rate instead of
/// computing its own (ADR-0052 clause 1). The board already said a rate in
/// words — <see cref="English.Flow"/>, *+412 last round · 240/s* — and the
/// prompt was the surface still saying a total.
/// <para>
/// A figure whose rate is not known is null, which every surface already shows
/// as absent rather than as zero: one publication is no interval, and *not
/// known yet* is not *stalled*. A figure that fell since the last publication
/// is not known either — a counter only falls where its publisher started over,
/// and two publications of two different runs are not an interval.
/// </para>
/// <para>
/// <see cref="Retrying"/> and <see cref="Failed"/> are rates for the same
/// reason the three stages are, and on the same words: the owner named all
/// five letters in the quote above, and T and F were left as totals on the
/// assistant's judgement rather than his. A retry total says a run has had
/// trouble; a retry rate says it is having trouble now, which is the one an
/// operator can act on. The total has not gone anywhere — <c>xmip-cli
/// measure</c> reports it, and a surface that wants *has this run had faults
/// at all* asks <see cref="Figures"/> rather than this (the owner,
/// 2026-09-20).
/// </para>
/// </remarks>
public sealed record FigureFlow(
    double? Streams,
    double? Journeys,
    double? Messages,
    double? Retrying = null,
    double? Failed = null)
{
    /// <summary>No interval, so no rate: what a reader has after one
    /// publication, and what a publisher that says no figures gives.</summary>
    public static FigureFlow Unknown { get; } = new(null, null, null);

    /// <summary>Whether any of the five rates is known.</summary>
    public bool Known => Streams is not null
        || Journeys is not null
        || Messages is not null
        || Retrying is not null
        || Failed is not null;

    /// <summary>
    /// The rate between two publications, over the time between them.
    /// <see cref="Unknown"/> where there is no interval to divide by.
    /// </summary>
    public static FigureFlow Between(Figures now, Figures? before, TimeSpan over)
    {
        ArgumentNullException.ThrowIfNull(now);

        if (before is null || over <= TimeSpan.Zero)
        {
            return Unknown;
        }

        double seconds = over.TotalSeconds;

        return new FigureFlow(
            Rate(now.Streams, before.Streams, seconds),
            Rate(now.Journeys, before.Journeys, seconds),
            Rate(now.Messages, before.Messages, seconds),
            Rate(now.Retrying, before.Retrying, seconds),
            Rate(now.Failed, before.Failed, seconds));
    }

    // A figure neither publication carries has no rate; nor has one that fell,
    // which is a publisher that started over rather than work undone.
    private static double? Rate(ulong? now, ulong? before, double seconds)
    {
        return now is { } count && before is { } then && count >= then
            ? (count - then) / seconds
            : null;
    }
}
