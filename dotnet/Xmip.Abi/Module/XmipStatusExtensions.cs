namespace Xmip.Abi.Module;

/// <summary>What a <see cref="XmipStatus"/> means to whoever holds it.</summary>
public static class XmipStatusExtensions
{
    /// <summary>
    /// Whether trying again could plausibly succeed.
    /// </summary>
    /// <remarks>
    /// A property of the code, not of the call site, so that
    /// xmip-core-resilience can decide without knowing the module. Timeout,
    /// Unavailable, Capacity and Again. Nothing else — and in particular not
    /// <see cref="XmipStatus.Io"/>, which covers faults that will repeat.
    /// </remarks>
    public static bool IsRetryable(this XmipStatus status)
    {
        return status
            is XmipStatus.Timeout
            or XmipStatus.Unavailable
            or XmipStatus.Capacity
            or XmipStatus.Again;
    }

    /// <summary>
    /// Whether the module instance is finished and must be destroyed.
    /// </summary>
    public static bool IsTerminal(this XmipStatus status)
    {
        return status
            is XmipStatus.Internal
            or XmipStatus.Panic;
    }

    /// <summary>One line an operator can read, for every code the header
    /// defines and one more for a code it does not.</summary>
    public static string Explain(this XmipStatus status)
    {
        return status switch
        {
            XmipStatus.Ok => "the call succeeded",

            XmipStatus.Invalid => "an argument was outside its contract",
            XmipStatus.Unsupported => "well formed, and not implemented here",
            XmipStatus.State => "the wrong lifecycle state for this call",
            XmipStatus.NotFound => "the thing asked for does not exist",

            XmipStatus.Malformed => "not the standard it claims to be",
            XmipStatus.Contract => "well formed, and violates the contract",
            XmipStatus.Truncated => "the stream ended mid-structure",

            XmipStatus.Io => "an input or output fault",
            XmipStatus.Timeout => "the call did not answer in time",
            XmipStatus.Unavailable => "the peer refused, or is down",
            XmipStatus.Auth => "authentication or authorisation was refused",
            XmipStatus.Capacity => "a quota, limit or resource was exhausted",

            XmipStatus.Cancelled => "the host asked for cancellation",
            XmipStatus.Again => "would block; not a failure",

            XmipStatus.Internal => "a defect in the module",
            XmipStatus.Panic => "unwinding was caught at the boundary",

            _ => "not a status this build knows",
        };
    }
}
