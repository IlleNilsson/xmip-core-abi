namespace Xmip.Abi.Operate;

/// <summary>
/// How the action an Event reports ended (runtime-model.md section 17). The
/// values are the header's <c>XMIP_OUTCOME_*</c> (section 11).
/// </summary>
public enum EventOutcome
{
    /// <summary>It succeeded.</summary>
    Success = 0,

    /// <summary>It failed.</summary>
    Failure = 1,

    /// <summary>It was rejected.</summary>
    Rejection = 2,

    /// <summary>It is waiting.</summary>
    Waiting = 3,

    /// <summary>It was paused.</summary>
    Pause = 4,

    /// <summary>It timed out.</summary>
    Timeout = 5,

    /// <summary>Its retries are exhausted.</summary>
    ExhaustedRetries = 6,

    /// <summary>It was dismissed.</summary>
    Dismissal = 7,
}
