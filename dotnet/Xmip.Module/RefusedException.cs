namespace Xmip.Module;

/// <summary>
/// What a unit throws to refuse, in words: a step's settings when it opens
/// (answered <c>XMIP_E_CONTRACT</c>), a Message's content when it is called
/// (answered <c>XMIP_E_MALFORMED</c>). The node reads the message as the
/// unit's last error. Anything else a unit throws is a defect in it,
/// answered <c>XMIP_E_INTERNAL</c>.
/// </summary>
public sealed class RefusedException : Exception
{
    /// <summary>A refusal without words; prefer one that says why.</summary>
    public RefusedException()
    {
    }

    /// <summary>A refusal saying why.</summary>
    /// <param name="message">Why, in one sentence.</param>
    public RefusedException(string message)
        : base(message)
    {
    }

    /// <summary>A refusal saying why, from what was thrown under it.</summary>
    /// <param name="message">Why, in one sentence.</param>
    /// <param name="innerException">What was thrown under it.</param>
    public RefusedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
