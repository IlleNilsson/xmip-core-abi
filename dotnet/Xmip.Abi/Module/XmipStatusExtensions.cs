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
}
