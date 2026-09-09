namespace Xmip.Abi.Operate;

/// <summary>One scope's health, how far from healthy, the line that explains
/// it, and when it was seen. Severity is 0–100: the mood says which, the number
/// shades it. Copied out of the snapshot, so it is safe to keep.</summary>
public sealed record HealthRecord(
    string Scope, HealthState State, byte Severity, string Evidence, DateTimeOffset Observed);
