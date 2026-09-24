namespace Xmip.Abi.Module;

/// <summary>
/// What one status code means, as every surface answers it: the code, whether
/// the header defines it, its name, the binding's one line of English, and
/// whether it is worth a retry or ends the module instance. <c>xmip-cli
/// status</c> renders it and <c>ConvertFrom-XmipStatus</c> emits it, so the
/// two cannot disagree about a code (ADR-0014; ADR-0052 clause 1). Until
/// 2026-09-24 each built its own, and a code the header does not define was
/// <c>unknown</c> on one and <c>Unknown</c> on the other.
/// </summary>
/// <param name="Code">The code as the boundary returned it.</param>
/// <param name="Known">Whether the header defines the code.</param>
/// <param name="Name">The header's name for it, or <c>unknown</c>.</param>
/// <param name="Meaning">One line an operator can read
/// (<see cref="XmipStatusExtensions.Explain"/>).</param>
/// <param name="Retryable">Whether trying again could succeed; never for a
/// code the header does not define.</param>
/// <param name="Terminal">Whether the module instance is finished; never for
/// a code the header does not define.</param>
public sealed record StatusMeaning(
    int Code, bool Known, string Name, string Meaning, bool Retryable, bool Terminal)
{
    /// <summary>The name of a code the header does not define.</summary>
    public const string Unknown = "unknown";

    /// <summary>What <paramref name="code"/> means. A code the header does
    /// not define is data, not an exception: it is said to be unknown.</summary>
    public static StatusMeaning Of(int code)
    {
        XmipStatus status = (XmipStatus)code;
        bool known = Enum.IsDefined(status);

        return new StatusMeaning(
            code,
            known,
            known ? status.ToString() : Unknown,
            status.Explain(),
            known && status.IsRetryable(),
            known && status.IsTerminal());
    }
}
