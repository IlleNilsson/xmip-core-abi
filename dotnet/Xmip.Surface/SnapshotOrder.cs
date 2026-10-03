using Xmip.Abi.Module;

namespace Xmip.Surface;

/// <summary>
/// An act a <see cref="SnapshotOperator"/> leaves for a node it reads
/// through its publication: one order for every noun — a Subscription, an
/// Event subscription, a Message in a Dead Message Queue — through
/// <c>xmip_order_v1</c> (<c>observe::Order</c>), and the one sentence every
/// surface says when it was left.
/// </summary>
internal static class SnapshotOrder
{
    /// <summary>
    /// Leave <paramref name="act"/> on the <paramref name="noun"/> called
    /// <paramref name="target"/> of <paramref name="node"/> where
    /// <paramref name="orders"/> says its publisher takes orders, for the
    /// node to take at its next look; declined where it says nowhere.
    /// <paramref name="what"/> names the target in the sentence said.
    /// </summary>
    public static (bool Left, string Said) Leave(
        string orders, string node, string noun, string target, string act, string who,
        string what)
    {
        if (orders.Length == 0)
        {
            return (false,
                "a snapshot is a record of what was published, and its publisher takes no orders");
        }

        bool left = RuntimeLibrary.Rules.Subscriptions.Order(
            orders, node, noun, target, act, who, out string said) == XmipStatus.Ok;

        return (left, left
            ? $"{act} of {what} left for {ScopeTree.Node(node)} to take at its next look"
            : said);
    }
}
