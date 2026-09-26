using Xmip.Abi.Operate;

namespace Xmip.Surface;

/// <summary>
/// The six measurements at a scope, said in the same order by every surface:
/// the four quantities ADR-0027 clause 5 keeps apart — Streams, Messages,
/// Journeys, bytes — and the two outcome counts its amendment of 2026-09-14
/// added, Retrying and Failed. A figure is null when the publisher has not
/// published it, and a surface shows that as absent rather than as zero.
/// </summary>
public sealed record Figures(
    string Scope,
    ulong? Streams,
    ulong? Messages,
    ulong? Journeys,
    ulong? Bytes,
    ulong? Retrying,
    ulong? Failed,
    DateTimeOffset? Observed)
{
    /// <summary>Nothing published at a scope.</summary>
    public static Figures None(string scope)
    {
        return new Figures(scope, null, null, null, null, null, null, null);
    }

    /// <summary>Read the six figures for a scope from a surface.</summary>
    public static Figures Read(IOperatorSurface surface, string scope)
    {
        MeasurementRecord? streams = surface.Measure(scope, Counted.Streams);
        MeasurementRecord? messages = surface.Measure(scope, Counted.Messages);
        MeasurementRecord? journeys = surface.Measure(scope, Counted.Journeys);
        MeasurementRecord? bytes = surface.Measure(scope, Counted.Bytes);
        MeasurementRecord? retrying = surface.Measure(scope, Counted.Retrying);
        MeasurementRecord? failed = surface.Measure(scope, Counted.Failed);

        MeasurementRecord?[] all = [streams, messages, journeys, bytes, retrying, failed];
        DateTimeOffset? observed = all
            .Where(record => record is not null)
            .Select(record => (DateTimeOffset?)record!.Observed)
            .Max();

        return new Figures(
            scope,
            streams?.Value,
            messages?.Value,
            journeys?.Value,
            bytes?.Value,
            retrying?.Value,
            failed?.Value,
            observed);
    }

    /// <summary>
    /// Several scopes' figures as one, under <paramref name="scope"/>: each
    /// figure the sum of the ones published, and absent where none of them
    /// published it — a figure nobody published does not become a zero by
    /// being added up.
    /// </summary>
    public static Figures Sum(string scope, IEnumerable<Figures> parts)
    {
        ArgumentNullException.ThrowIfNull(parts);

        Figures[] all = [.. parts];

        ulong? Total(Func<Figures, ulong?> figure)
        {
            ulong[] published = [.. all.Select(figure).OfType<ulong>()];

            return published.Length == 0
                ? null
                : published.Aggregate(0UL, (sum, value) => sum + value);
        }

        return new Figures(
            scope,
            Total(part => part.Streams),
            Total(part => part.Messages),
            Total(part => part.Journeys),
            Total(part => part.Bytes),
            Total(part => part.Retrying),
            Total(part => part.Failed),
            all.Select(part => part.Observed).Max());
    }

    /// <summary>Whether the publisher supplied at least one figure.</summary>
    public bool HasValues =>
        Streams.HasValue || Messages.HasValue || Journeys.HasValue
        || Bytes.HasValue || Retrying.HasValue || Failed.HasValue;
}
