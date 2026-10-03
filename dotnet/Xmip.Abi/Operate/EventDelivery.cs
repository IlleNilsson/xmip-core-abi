namespace Xmip.Abi.Operate;

/// <summary>
/// What one drain of a subscription handed over: the Events, oldest first;
/// how many a full queue refused since the drain before (section 11's
/// <c>*out_refused</c>); and the members of the cluster not heard when it
/// was drained, whose Events cannot be among them (ADR-0065, amendment
/// 2026-10-02). A drain wakes for a change of those alone, with no Event.
/// Empty when nothing arrived in time.
/// </summary>
/// <param name="Events">The Events, copied out.</param>
/// <param name="Refused">Events a full queue refused.</param>
/// <param name="Unheard">Every member not heard now.</param>
/// <param name="UnheardChanged">Whether that changed since the drain before.</param>
public sealed record EventDelivery(
    IReadOnlyList<EventRecord> Events,
    ulong Refused,
    IReadOnlyList<UnheardRecord> Unheard,
    bool UnheardChanged)
{
    /// <summary>Nothing arrived.</summary>
    public static EventDelivery Nothing { get; } = new([], 0, [], false);
}
