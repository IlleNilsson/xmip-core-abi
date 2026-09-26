namespace Xmip.Abi.Operate;

/// <summary>
/// What one drain of a subscription handed over: the Events, oldest first,
/// and how many a full queue refused since the drain before (section 11's
/// <c>*out_refused</c>). Empty when nothing arrived in time.
/// </summary>
/// <param name="Events">The Events, copied out.</param>
/// <param name="Refused">Events a full queue refused.</param>
public sealed record EventDelivery(IReadOnlyList<EventRecord> Events, ulong Refused)
{
    /// <summary>Nothing arrived.</summary>
    public static EventDelivery Nothing { get; } = new([], 0);
}
