using Xmip.Abi.Operate;

namespace Xmip.Surface;

/// <summary>
/// What an Operator does to a Message in a Dead Message Queue (ADR-0052,
/// amendment 2026-10-01): <c>observe::Act</c> as
/// <c>observe::Noun::DeadMessage</c> takes it, whose word — the name, lower
/// case — is what crosses to the runtime. There is one.
/// </summary>
public enum DeadMessageAct
{
    /// <summary>Route its promoted properties against the node's
    /// Subscriptions of now, open a Journey for each match and take the
    /// entry out, once; a Message that still matches nothing stays.</summary>
    Replay,
}

/// <summary>
/// What came of a <see cref="DeadMessageAct"/> on the Message
/// <paramref name="Message"/> in the Dead Message Queue of
/// <paramref name="Node"/>: whether it was applied — or, through a
/// publication, left for the node — and what was said. The executable and
/// the PowerShell module emit this one shape, as they emit a
/// <see cref="SubscriptionOperation"/>.
/// </summary>
public sealed record DeadMessageOperation(
    string Node, string Message, DeadMessageAct Act, bool Applied, string Result)
{
    /// <summary>The noun an order on a Message in a Dead Message Queue is
    /// left under: <c>observe::Noun::DeadMessage</c>'s word.</summary>
    public const string Noun = "dead-message";

    /// <summary>The word the runtime names <paramref name="act"/> by.</summary>
    public static string Word(DeadMessageAct act)
    {
        return act.ToString().ToLowerInvariant();
    }

    /// <summary>The act not taken, and why.</summary>
    public static DeadMessageOperation Declined(
        DeadMessageRecord message, DeadMessageAct act, string why)
    {
        ArgumentNullException.ThrowIfNull(message);

        return new DeadMessageOperation(message.Node, message.Message, act, false, why);
    }
}
