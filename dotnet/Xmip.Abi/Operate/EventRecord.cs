namespace Xmip.Abi.Operate;

/// <summary>
/// One Event, copied out of the runtime (<see cref="XmipEvent"/>, section 11
/// of <c>xmip_operate.h</c>) or handed to it to publish: references, never a
/// payload. Identifiers are UUIDs as text, as the runtime writes and reads
/// them; every optional string is empty where there is none. What each means
/// and whether it is well formed is the runtime's to say, never this
/// record's.
/// </summary>
public sealed record EventRecord
{
    /// <summary>The Event's identifier. Empty when publishing: the runtime
    /// mints one.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Its type, such as <c>se.xmip.receive.failure</c>.</summary>
    public required string Type { get; init; }

    /// <summary>When it happened, in nanoseconds since the Unix epoch. 0 when
    /// publishing is now.</summary>
    public long TimeUnixNanos { get; init; }

    /// <summary>The action it completed.</summary>
    public EventAction Action { get; init; }

    /// <summary>How the action ended.</summary>
    public EventOutcome Outcome { get; init; }

    /// <summary>The Xmip URI it happened at.</summary>
    public required string Scope { get; init; }

    /// <summary>The Journey it belongs to.</summary>
    public string Journey { get; init; } = string.Empty;

    /// <summary>The Message it is about.</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>The Stream it is about.</summary>
    public string Stream { get; init; } = string.Empty;

    /// <summary>The endpoint it happened at.</summary>
    public string Endpoint { get; init; } = string.Empty;

    /// <summary>The Module that did it.</summary>
    public string Module { get; init; } = string.Empty;

    /// <summary>The artifact it concerns.</summary>
    public string Artifact { get; init; } = string.Empty;

    /// <summary>The Party it is about.</summary>
    public string Party { get; init; } = string.Empty;

    /// <summary>Its diagnostics, name to value, safe to hand outside.</summary>
    public IReadOnlyDictionary<string, string> Diagnostics { get; init; } =
        new Dictionary<string, string>();

    /// <summary><see cref="TimeUnixNanos"/> as a point in time, to the
    /// tick.</summary>
    public DateTimeOffset Time =>
        DateTimeOffset.UnixEpoch.AddTicks(TimeUnixNanos / 100);
}
