using Xmip.Abi.Operate;

namespace Xmip.Surface;

/// <summary>
/// The five operational figures every Xmip surface says in the same order.
/// Flow nouns retain their domain units: received Streams, processed Journeys,
/// sent Messages, and the two outcome counts Retrying and Failed.
/// </summary>
public sealed record ActivitySummary(
    string Scope,
    ulong? Received,
    ulong? Processed,
    ulong? Sent,
    ulong? Retrying,
    ulong? Failed,
    DateTimeOffset? Observed)
{
    /// <summary>Read one summary from a published operator snapshot.</summary>
    public static ActivitySummary Read(IOperatorSurface surface, string scope)
    {
        MeasurementRecord? received = surface.Measure(scope, Counted.Streams);
        MeasurementRecord? processed = surface.Measure(scope, Counted.Journeys);
        MeasurementRecord? sent = surface.Measure(scope, Counted.Messages);
        MeasurementRecord? retrying = surface.Measure(scope, Counted.Retrying);
        MeasurementRecord? failed = surface.Measure(scope, Counted.Failed);

        DateTimeOffset? observed = new[] { received, processed, sent, retrying, failed }
            .Where(value => value is not null)
            .Select(value => (DateTimeOffset?)value!.Observed)
            .Max();

        return new ActivitySummary(
            scope,
            received?.Value,
            processed?.Value,
            sent?.Value,
            retrying?.Value,
            failed?.Value,
            observed);
    }

    /// <summary>Whether the publisher supplied at least one figure.</summary>
    public bool HasValues =>
        Received.HasValue || Processed.HasValue || Sent.HasValue
        || Retrying.HasValue || Failed.HasValue;
}
