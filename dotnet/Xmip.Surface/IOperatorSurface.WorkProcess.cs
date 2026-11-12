using Xmip.Abi.Operate;

namespace Xmip.Surface;

// Whether a Work Process is placed: what the Work Process card says while the
// process stage holds Subscriptions alone (ADR-0052, amendment 2026-11-11).
public partial interface IOperatorSurface
{
    /// <summary>
    /// Whether <paramref name="stage"/> is the stage a node publishes its
    /// Subscriptions beneath and everything configured there is one of them:
    /// no Work Process is placed on any node. The card titled Work Process
    /// then says so, beside the Subscriptions it counts, rather than teach
    /// that a Subscription is a Work Process. A Subscription is known by its
    /// node and name in the Subscriptions the publication lists, never by
    /// its name's words.
    /// </summary>
    public bool NoWorkProcessPlaced(string stage)
    {
        IReadOnlyList<SubscriptionRecord> listed = Subscriptions().Subscriptions;
        HashSet<string> subscriptions =
        [
            .. listed.Select(one =>
                ScopeTree.Join([.. ScopeTree.Parts(one.Node), stage, one.Name])),
        ];
        ScopeIndex index = Index();
        IReadOnlyList<string> configured =
        [
            .. index.StageScopes(stage)
                .SelectMany(index.Branches)
                .Select(branch => branch.Scope),
        ];

        return configured.Count > 0 && configured.All(subscriptions.Contains);
    }
}
