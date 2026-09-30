using Xmip.Abi.Operate;

namespace Xmip.Surface;

/// <summary>
/// What an operator does to an Event subscription (ADR-0065, amendment
/// 2026-09-29): <c>observe::Act</c> as <c>observe::Noun::EventSubscription</c>
/// takes it, whose word — the name, lower case — is what crosses to the
/// runtime, which refuses any other.
/// </summary>
public enum EventSubscriptionAct
{
    /// <summary>Hold delivery; the queue keeps filling up to its capacity.</summary>
    Pause,

    /// <summary>Deliver again, what queued first.</summary>
    Resume,

    /// <summary>Unsubscribe it.</summary>
    Remove,
}

/// <summary>
/// What came of an <see cref="EventSubscriptionAct"/> on the Event subscription numbered
/// <paramref name="Id"/> on <paramref name="Node"/>: whether it was applied
/// — or, through a publication, left for the node — and what was said. The
/// executable and the PowerShell module emit this one shape, as they emit a
/// <see cref="ScopeOperation"/>.
/// </summary>
public sealed record EventSubscriptionOperation(
    string Node, ulong Id, EventSubscriptionAct Act, bool Applied, string Result)
{
    /// <summary>The word the runtime names <paramref name="act"/> by.</summary>
    public static string Word(EventSubscriptionAct act)
    {
        return act.ToString().ToLowerInvariant();
    }

    /// <summary>The act not taken, and why.</summary>
    public static EventSubscriptionOperation Declined(
        EventSubscriptionRecord subscription, EventSubscriptionAct act, string why)
    {
        ArgumentNullException.ThrowIfNull(subscription);

        return new EventSubscriptionOperation(subscription.Node, subscription.Id, act, false, why);
    }
}
