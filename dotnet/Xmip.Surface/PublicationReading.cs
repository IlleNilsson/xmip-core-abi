using Xmip.Abi.Operate;

namespace Xmip.Surface;

/// <summary>
/// A publication as a surface answers from it: the text a node published —
/// read from its file by <see cref="SnapshotOperator"/>, or over the node's
/// operate listener by <see cref="RemoteOperator"/> — read once by the
/// runtime's one reader (<c>xmip_operate.h</c> section 8) and laid out as
/// the records every screen, command and cmdlet reads. Written once for
/// both.
/// </summary>
internal sealed record PublicationReading(
    ScopeIndex Index,
    TopologySnapshot Topology,
    RunHeader Run,
    string Root,
    SubscriptionList Subscriptions,
    EventSubscriptionList EventSubscriptions,
    DeadMessageList DeadMessages,
    FailedJourneyList FailedJourneys)
{
    /// <summary>Nothing published, said by <paramref name="source"/>.</summary>
    public static PublicationReading Nothing(string source)
    {
        return new PublicationReading(
            ScopeIndex.Empty(source),
            TopologySnapshot.Empty(source),
            RunHeader.None,
            ScopeTree.Root,
            SubscriptionList.Empty,
            EventSubscriptionList.Empty,
            DeadMessageList.Empty,
            FailedJourneyList.Unlisted);
    }

    /// <summary>
    /// <paramref name="text"/> read as revision <paramref name="revision"/>;
    /// its own source where it names one, <paramref name="source"/>
    /// otherwise. Text the runtime's reader refuses is nothing published.
    /// </summary>
    public static PublicationReading Of(string text, ulong revision, string source)
    {
        if (RuntimeLibrary.Rules.Publications.Read(text, out _) is not { } read)
        {
            return Nothing(source);
        }

        string said = read.Source.Length > 0 ? read.Source : source;

        // Each count keeps when it was observed, so a figure is as old as its
        // publication says: until 2026-09-26 it was dropped here, and every
        // figure a snapshot answered was dated now, however long the
        // publisher had been silent (ADR-0027 clause 6).
        ScopeIndex index = ScopeIndex.Build(
            read.Records,
            read.Counts.Select(count => new ScopeIndex.Count(
                count.Scope, count.Counted, count.Value, count.Observed)),
            revision,
            said);

        // Where a drill starts: the document's node — a Playground roll's
        // cluster — where a record is published beneath it; the root when it
        // names none, or names a scope nothing is published at.
        string root = read.Node.Length > 0 && index.Health(read.Node).Count > 0
            ? read.Node
            : ScopeTree.Root;

        return new PublicationReading(
            index,
            read.Topology ?? TopologySnapshot.Empty(said),
            RunHeader.From(read.Run),
            root,
            read.Subscriptions,
            read.EventSubscriptions,
            read.DeadMessages,
            read.FailedJourneys);
    }

    /// <summary>
    /// The Journeys that failed at or beneath <paramref name="scope"/> as the
    /// publication carries them — each Port's count and its window of the
    /// oldest — those from the place <paramref name="from"/> in the Port's
    /// queue on, at most <paramref name="most"/> of each Port (0: every one
    /// carried). A publication carries no place in the Port's index of failed
    /// Journeys, which a node's own list pages by, so its list names no next
    /// (<see cref="IOperatorSurface.FailedJourneys"/>).
    /// </summary>
    public FailedJourneyList FailedJourneysWithin(string scope, ulong from, uint most)
    {
        FailedJourneyList within = JourneyOperation.Within(FailedJourneys, scope);
        int take = most == 0 ? int.MaxValue : (int)Math.Min(most, int.MaxValue);

        return within with
        {
            Ports =
            [
                .. within.Ports.Select(port => port with
                {
                    Journeys = [.. port.Journeys.Where(j => j.Sequence >= from).Take(take)],
                }),
            ],
        };
    }
}
