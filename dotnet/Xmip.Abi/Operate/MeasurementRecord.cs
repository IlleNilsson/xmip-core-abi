namespace Xmip.Abi.Operate;

/// <summary>One count over a window, and when it was taken. Copied out of the
/// snapshot, so it is safe to keep.</summary>
public sealed record MeasurementRecord(
    string Scope,
    Counted Counted,
    ulong Value,
    DateTimeOffset WindowStart,
    DateTimeOffset WindowEnd,
    DateTimeOffset Observed);
