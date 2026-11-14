using Xmip.Abi.Module;

namespace Xmip.Surface;

/// <summary>
/// What came of an operator's act at a node's door (ADR-0014, amendment
/// 2026-11-13): taken by the node it is asked of; asked — written to Xmip
/// Storage and not taken yet within the door's bound, its node taking it as
/// soon as it runs; or refused, by role, by the door or by the node. Every
/// act's record carries it, so the command line, the PowerShell module and
/// the GUIs say the same word.
/// </summary>
public enum ActOutcome
{
    /// <summary>Taken by the node it is asked of.</summary>
    Taken,

    /// <summary>Asked of its node and not taken yet; never lost.</summary>
    Asked,

    /// <summary>Not taken: refused, invalid, failed or unanswered.</summary>
    Refused,
}

/// <summary>The outcome a status of <c>xmip_remote_act_v1</c> says, and its
/// word.</summary>
public static class ActOutcomes
{
    /// <summary>
    /// <see cref="ActOutcome.Taken"/> for <c>XMIP_OK</c>,
    /// <see cref="ActOutcome.Asked"/> for <c>XMIP_E_AGAIN</c> — asked, not a
    /// failure — and <see cref="ActOutcome.Refused"/> for any other.
    /// </summary>
    public static ActOutcome Of(XmipStatus status)
    {
        return status == XmipStatus.Ok
            ? ActOutcome.Taken
            : status == XmipStatus.Again ? ActOutcome.Asked : ActOutcome.Refused;
    }

    /// <summary>The word a surface says it by: <c>taken</c>, <c>asked</c>,
    /// <c>refused</c>.</summary>
    public static string Word(this ActOutcome outcome)
    {
        return outcome.ToString().ToLowerInvariant();
    }

    /// <summary>
    /// What a command says of an act that came to <paramref name="outcome"/>,
    /// saying <paramref name="result"/>, its outcome first in words:
    /// <c>OK.</c>, <c>ASKED.</c> or <c>REFUSED:</c>.
    /// </summary>
    public static string Said(ActOutcome outcome, string result)
    {
        return outcome switch
        {
            ActOutcome.Taken => $"OK. {result}",
            ActOutcome.Asked => $"ASKED. {result}",
            ActOutcome.Refused => $"REFUSED: {result}",
            _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null),
        };
    }
}
