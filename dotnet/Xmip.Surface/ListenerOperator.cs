using Xmip.Abi.Module;
using Xmip.Abi.Operate;

namespace Xmip.Surface;

/// <summary>
/// The surface over a node's operate listener, followed over gRPC (ADR-0014,
/// amendments 2026-10-12 and 2026-10-13; ADR-0052, amendment 2026-10-05):
/// the runtime's surface, <c>xmip_operate.h</c> section 17, which the
/// command line, the PowerShell module and the GUIs all operate a node
/// through — <see cref="LocalOperator"/> on this machine, at the node's local
/// endpoint, and <see cref="RemoteOperator"/> on another, at its operate
/// address inside Xmip's mutual TLS. SignalR stays between a web host and
/// its browser.
/// </summary>
/// <remarks>
/// It reads the node's publication — the text its file holds, read by the
/// one reader (<see cref="PublicationReading"/>) — once per revision, and is
/// told, never asks, when it advances: the runtime's follow holds the node's
/// wait. A watch is told first and the publication read after, by the first
/// question that needs it, off the follow: until 2026-10-13 the follow read
/// and parsed the whole publication before it told, and a .NET watch heard
/// of a change 1.7 ms after the node posted it. A lost node is told too:
/// every watch at once, with a <see cref="Source"/> that says the node is
/// unreachable and why, and every answer is empty until it is back — never
/// the publication held from before. The follow tries a lost node again on
/// its own loop (ADR-0052, note of 2026-10-05), in the runtime.
///
/// An act crosses without a name: the node takes it as the identity the
/// connection proved — this side's certificate, or on the local endpoint
/// the user this process runs as — admitted by the role the node grants
/// it, and refuses it in words otherwise (ADR-0009). The <c>who</c> an act
/// is given here does not leave this process.
/// </remarks>
public abstract class ListenerOperator : IOperatorSurface, IDisposable
{
    /// <summary>How long a watch's one wait lasts before it looks whether
    /// the watch ended; a change wakes it at once.</summary>
    private static readonly TimeSpan Turn = TimeSpan.FromSeconds(1);

    private readonly RemoteNode? node;

    private readonly ChangeFeed feed = new();

    private readonly Lock gate = new();

    // One read of the node at a time, so a change told is read once.
    private readonly Lock reading = new();

    private PublicationReading? held;

    private ulong heldRevision;

    // The revision the follow last told.
    private ulong toldRevision;

    private Thread? following;

    private volatile bool ended;

    /// <summary>A surface over <paramref name="node"/>, called
    /// <paramref name="address"/> in what it says, of the
    /// <paramref name="kind"/> its <see cref="Source"/> opens with; with no
    /// node, <paramref name="reason"/> says why.</summary>
    private protected ListenerOperator(
        string kind, string address, RemoteNode? node, string reason)
    {
        Kind = kind;
        this.node = node;
        Address = address;
        Reason = node is null ? reason : "not connected yet";
    }

    /// <summary>Where the node is, as <see cref="Source"/> opens: <c>LOCAL</c>
    /// on this machine, <c>REMOTE</c> on another.</summary>
    public string Kind { get; }

    /// <summary>Where the node is reached: its operate address, or its local
    /// endpoint.</summary>
    public string Address { get; }

    /// <summary>Whether the node answered when last asked or told.</summary>
    public bool IsConnected { get; private set; }

    /// <summary>Why the node does not answer, when it does not.</summary>
    public string Reason { get; private set; }

    /// <inheritdoc />
    public string Source => IsConnected
        ? $"{Kind} — {Address}"
        : $"{Kind} — {Address} unreachable: {Reason}";

    /// <summary>Ask the node for its publication: whether it answered, why
    /// not in <see cref="Reason"/>.</summary>
    public bool Connect()
    {
        _ = Read();
        return IsConnected;
    }

    /// <inheritdoc />
    public ScopeIndex Index()
    {
        return Read().Index;
    }

    /// <inheritdoc />
    public IReadOnlyList<HealthRecord> Health(string scope)
    {
        return Index().Health(scope);
    }

    /// <inheritdoc />
    public Figures Figures(string scope)
    {
        return Index().Figures(scope);
    }

    /// <inheritdoc />
    public MeasurementRecord? Measure(string scope, Counted counted)
    {
        return Index().Measure(scope, counted);
    }

    /// <inheritdoc />
    public TopologySnapshot Topology()
    {
        return Read().Topology;
    }

    /// <inheritdoc />
    public RunHeader Run()
    {
        return Read().Run;
    }

    /// <inheritdoc />
    public string Root()
    {
        return Read().Root;
    }

    /// <inheritdoc />
    public EventSubscriptionList EventSubscriptions()
    {
        return Read().EventSubscriptions;
    }

    /// <inheritdoc />
    public EventSubscriptionOperation Act(
        EventSubscriptionRecord subscription, EventSubscriptionAct act, string who)
    {
        ArgumentNullException.ThrowIfNull(subscription);

        (bool taken, string said) = Asked(
            subscription.Node, "event-subscription",
            subscription.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
            EventSubscriptionOperation.Word(act));

        return new EventSubscriptionOperation(subscription.Node, subscription.Id, act, taken, said);
    }

    /// <inheritdoc />
    public SubscriptionList Subscriptions()
    {
        return Read().Subscriptions;
    }

    /// <inheritdoc />
    public SubscriptionOperation Act(
        SubscriptionRecord subscription, SubscriptionAct act, string who)
    {
        ArgumentNullException.ThrowIfNull(subscription);

        (bool taken, string said) = Asked(
            subscription.Node, "subscription", subscription.Name, SubscriptionOperation.Word(act));

        return new SubscriptionOperation(subscription.Node, subscription.Name, act, taken, said);
    }

    /// <inheritdoc />
    public DeadMessageList DeadMessages()
    {
        return Read().DeadMessages;
    }

    /// <inheritdoc />
    public DeadMessageOperation Act(DeadMessageRecord message, DeadMessageAct act, string who)
    {
        ArgumentNullException.ThrowIfNull(message);

        (bool taken, string said) = Asked(
            message.Node,
            DeadMessageOperation.Noun,
            message.Message,
            DeadMessageOperation.Word(act));

        return new DeadMessageOperation(message.Node, message.Message, act, taken, said);
    }

    /// <inheritdoc />
    /// <remarks>What the node's publication carries: each Port's count and
    /// its window of the oldest, <paramref name="from"/> a place in the
    /// Port's queue and no next (<see cref="IOperatorSurface.FailedJourneys"/>
    /// says why).</remarks>
    public FailedJourneyList FailedJourneys(string scope, ulong from = 0, uint most = 0)
    {
        return Read().FailedJourneysWithin(scope, from, most);
    }

    /// <inheritdoc />
    public JourneyOperation Act(string scope, string journey, JourneyAct act, string who)
    {
        return JourneyOperation.OnNode(scope, journey, act, at =>
        {
            (bool taken, string said) = Asked(
                at, JourneyOperation.Noun, journey, JourneyOperation.Word(act));

            return new JourneyOperation(at, journey, act, taken, said);
        });
    }

    /// <inheritdoc />
    public string PauseScope(string scope, string who)
    {
        return Control(scope, ScopeAction.Pause, who).Result;
    }

    /// <inheritdoc />
    public string ResumeScope(string scope)
    {
        return Control(scope, ScopeAction.Resume, string.Empty).Result;
    }

    /// <inheritdoc />
    /// <remarks>A node's operate listener takes the acts on what it
    /// publishes, not a pause of a scope: declined in words.</remarks>
    public ScopeOperation Control(string scope, ScopeAction action, string who)
    {
        return new ScopeOperation(
            scope, action, false,
            $"{Address} takes no pause or resume of a scope over its operate listener");
    }

    /// <inheritdoc />
    public IAsyncEnumerable<SurfaceChange> WatchAsync(CancellationToken stop = default)
    {
        return feed.Watch(() => SurfaceChange.Initial(Source), Followed, stop);
    }

    // The follow starts with the first watch and runs while this lives.
    private void Followed()
    {
        lock (gate)
        {
            if (node is not null && following is null)
            {
                following = new Thread(Follow) { IsBackground = true, Name = "xmip-operate" };
                following.Start();
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>End every watch, the follow and the node's handle.</summary>
    protected virtual void Dispose(bool disposing)
    {
        if (!disposing || ended)
        {
            return;
        }

        ended = true;
        feed.Complete();
        node?.Dispose();
        following?.Join();
    }

    // The publication, read once per revision the follow told, by the first
    // question after it told; read on every question where nothing follows.
    // Unreachable, nothing — never what was held from before.
    private PublicationReading Read()
    {
        if (node is null)
        {
            IsConnected = false;
            return PublicationReading.Nothing(Source);
        }

        lock (reading)
        {
            lock (gate)
            {
                if (following is not null && held is { } current && IsConnected)
                {
                    return current;
                }
            }

            XmipStatus status = node.Publication(out string text, out ulong revision);

            if (status != XmipStatus.Ok)
            {
                IsConnected = false;
                Reason = text;
                return PublicationReading.Nothing(Source);
            }

            IsConnected = true;
            Reason = string.Empty;

            lock (gate)
            {
                if (held is { } same && heldRevision == revision)
                {
                    return same;
                }

                PublicationReading read = PublicationReading.Of(text, revision, Source);

                // A reading older than what the follow has since told is
                // answered, not kept: the next question reads the newer.
                if (revision >= Volatile.Read(ref toldRevision))
                {
                    held = read;
                    heldRevision = revision;
                }

                return read;
            }
        }
    }

    private (bool Taken, string Said) Asked(string at, string noun, string target, string act)
    {
        if (node is null)
        {
            return (false, Source);
        }

        XmipStatus status = node.Act(at, noun, target, act, out string said);

        if (status == XmipStatus.Unavailable)
        {
            IsConnected = false;
            Reason = said;
            return (false, Source);
        }

        return (status == XmipStatus.Ok, said);
    }

    // The runtime's follow, waited on: each time it tells, the publication
    // held goes and every watch is told at once; the publication is read by
    // whoever asks next, never before the telling.
    private void Follow()
    {
        ulong seen = 0;

        while (!ended && node is not null)
        {
            RemoteFollow told = node.Wait(seen, Turn);

            if (ended || told.Told == seen)
            {
                continue;
            }

            seen = told.Told;

            lock (gate)
            {
                Volatile.Write(ref toldRevision, told.Revision);
                held = null;
            }

            IsConnected = told.Reachable;
            Reason = told.Reachable ? string.Empty : told.Reason;

            feed.Announce(new SurfaceChange(
                told.Revision, SurfaceChangeKind.All, DateTimeOffset.UtcNow, Source));
        }
    }
}
