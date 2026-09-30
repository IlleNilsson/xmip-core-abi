using Xmip.Abi.Operate;

namespace Xmip.Surface;

/// <summary>
/// What an operator does to a Subscription (ADR-0013, amendment
/// 2026-09-30): <c>observe::Act</c> as <c>observe::Noun::Subscription</c>
/// takes it, whose word — the name, lower case — is what crosses to the
/// runtime. There is no remove: a Subscription is added and removed in the
/// TOML configuration of the Xmip Application that draws it, and the
/// runtime refuses the word.
/// </summary>
public enum SubscriptionAct
{
    /// <summary>Hold what it matches: kept, counted, not picked up.</summary>
    Pause,

    /// <summary>Pick up what it held, oldest first, and what it matches.</summary>
    Resume,
}

/// <summary>
/// What came of a <see cref="SubscriptionAct"/> on the Subscription called
/// <paramref name="Name"/> on <paramref name="Node"/>: whether it was
/// applied — or, through a publication, left for the node — and what was
/// said. The executable and the PowerShell module emit this one shape, as
/// they emit a <see cref="ScopeOperation"/>.
/// </summary>
public sealed record SubscriptionOperation(
    string Node, string Name, SubscriptionAct Act, bool Applied, string Result)
{
    /// <summary>What a surface says where a Subscription would be removed:
    /// it is not removed by an act.</summary>
    public const string Configured =
        "A Subscription is added and removed in the TOML configuration of the Xmip " +
        "Application that draws it; an operator pauses and resumes it.";

    /// <summary>The word the runtime names <paramref name="act"/> by.</summary>
    public static string Word(SubscriptionAct act)
    {
        return act.ToString().ToLowerInvariant();
    }

    /// <summary>The act not taken, and why.</summary>
    public static SubscriptionOperation Declined(
        SubscriptionRecord subscription, SubscriptionAct act, string why)
    {
        ArgumentNullException.ThrowIfNull(subscription);

        return new SubscriptionOperation(subscription.Node, subscription.Name, act, false, why);
    }
}
