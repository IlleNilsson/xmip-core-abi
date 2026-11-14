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
/// It reads the node's publication — or, where the node gathers its
/// cluster's view, that view — once per revision, and is told, never asks,
/// when it advances (<c>ListenerOperator.Follow.cs</c> says how, and how
/// which of the two is decided).
///
/// An act crosses without a name: the node takes it as the identity the
/// connection proved — this side's certificate, or on the local endpoint
/// the user this process runs as — admitted by the role the node grants
/// it, and refuses it in words otherwise (ADR-0009). The <c>who</c> an act
/// is given here does not leave this process; a <c>caller</c> does: the node
/// is handed it and decides by its own role (ADR-0009, amendment 2026-11-01).
/// </remarks>
public abstract partial class ListenerOperator : IOperatorSurface, IDisposable
{
    private readonly RemoteNode? node;

    private readonly ChangeFeed feed = new();

    private readonly Lock gate = new();

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
    /// <remarks>Where the node gathers its cluster's view, what that view says
    /// of itself follows — <c>&lt;cluster&gt;, gathered by &lt;node&gt;</c> and
    /// how many it reached, the runtime's words and never this one's.</remarks>
    public string Source => !IsConnected
        ? $"{Kind} — {Address} unreachable: {Reason}"
        : Gathered.Length > 0
            ? $"{Kind} — {Address} — {Gathered}"
            : $"{Kind} — {Address}";

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
        EventSubscriptionRecord subscription, EventSubscriptionAct act, string who,
        HeldCaller? caller)
    {
        ArgumentNullException.ThrowIfNull(subscription);

        (ActOutcome outcome, string said) = Asked(
            caller,
            subscription.Node, "event-subscription",
            subscription.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
            EventSubscriptionOperation.Word(act));

        return new EventSubscriptionOperation(
            subscription.Node, subscription.Id, act, outcome == ActOutcome.Taken, said)
        {
            Outcome = outcome,
        };
    }

    /// <inheritdoc />
    public SubscriptionList Subscriptions()
    {
        return Read().Subscriptions;
    }

    /// <inheritdoc />
    public SubscriptionOperation Act(
        SubscriptionRecord subscription, SubscriptionAct act, string who, HeldCaller? caller)
    {
        ArgumentNullException.ThrowIfNull(subscription);

        (ActOutcome outcome, string said) = Asked(
            caller, subscription.Node, "subscription", subscription.Name,
            SubscriptionOperation.Word(act));

        return new SubscriptionOperation(
            subscription.Node, subscription.Name, act, outcome == ActOutcome.Taken, said)
        {
            Outcome = outcome,
        };
    }

    /// <inheritdoc />
    public DeadMessageList DeadMessages()
    {
        return Read().DeadMessages;
    }

    /// <inheritdoc />
    public DeadMessageOperation Act(
        DeadMessageRecord message, DeadMessageAct act, string who, HeldCaller? caller)
    {
        ArgumentNullException.ThrowIfNull(message);

        (ActOutcome outcome, string said) = Asked(
            caller,
            message.Node,
            DeadMessageOperation.Noun,
            message.Message,
            DeadMessageOperation.Word(act));

        return new DeadMessageOperation(
            message.Node, message.Message, act, outcome == ActOutcome.Taken, said)
        {
            Outcome = outcome,
        };
    }

    /// <inheritdoc />
    /// <remarks>What the node's publication carries: each Port's count and
    /// its window of the oldest, <paramref name="from"/> a place in the
    /// Port's queue and no next (<see cref="IOperatorSurface.FailedMessages"/>
    /// says why).</remarks>
    public FailedMessageList FailedMessages(string scope, ulong from = 0, uint most = 0)
    {
        return Read().FailedMessagesWithin(scope, from, most);
    }

    /// <inheritdoc />
    public MessageOperation Act(
        string scope, string journey, MessageAct act, string who, HeldCaller? caller)
    {
        return MessageOperation.OnNode(scope, journey, act, at =>
        {
            (ActOutcome outcome, string said) = Asked(
                caller, at, MessageOperation.Noun, journey, MessageOperation.Word(act));

            return new MessageOperation(at, journey, act, outcome == ActOutcome.Taken, said)
            {
                Outcome = outcome,
            };
        });
    }

    /// <inheritdoc />
    public string PauseScope(string scope, string who, HeldCaller? caller)
    {
        return Control(scope, ScopeAction.Pause, who, caller).Result;
    }

    /// <inheritdoc />
    public string ResumeScope(string scope, HeldCaller? caller)
    {
        return Control(scope, ScopeAction.Resume, string.Empty, caller).Result;
    }

    /// <inheritdoc />
    /// <remarks>A node's operate listener takes the acts on what it
    /// publishes, not a pause of a scope: declined in words.</remarks>
    public ScopeOperation Control(
        string scope, ScopeAction action, string who, HeldCaller? caller)
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

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private (ActOutcome Outcome, string Said) Asked(
        HeldCaller? caller, string at, string noun, string target, string act)
    {
        if (node is null)
        {
            return (ActOutcome.Refused, Source);
        }

        XmipStatus status = node.Act(caller, at, noun, target, act, out string said);

        if (status == XmipStatus.Unavailable)
        {
            IsConnected = false;
            Reason = said;
            return (ActOutcome.Refused, Source);
        }

        return (ActOutcomes.Of(status), said);
    }
}
