namespace Xmip.Abi.Operate;

/// <summary>
/// Which Events a subscription asks for (<see cref="XmipEventFilter"/>,
/// section 11 of <c>xmip_operate.h</c>). Each is empty for any. Whether an
/// Event matches is the runtime's rule (<c>xmip-core-event</c>), never
/// decided here.
/// </summary>
public sealed record EventFilter
{
    /// <summary>A filter that asks for every Event.</summary>
    public static EventFilter Everything { get; } = new();

    /// <summary>The Event types wanted.</summary>
    public IReadOnlyList<string> Types { get; init; } = [];

    /// <summary>The outcomes wanted.</summary>
    public IReadOnlyList<EventOutcome> Outcomes { get; init; } = [];

    /// <summary>The Xmip URI an Event must have happened at or beneath.</summary>
    public string Scope { get; init; } = string.Empty;

    /// <summary>The Party's UUID an Event must be about.</summary>
    public string Party { get; init; } = string.Empty;
}
