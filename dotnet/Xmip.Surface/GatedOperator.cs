using Xmip.Abi.Operate;

namespace Xmip.Surface;

/// <summary>
/// A surface whose acts are taken only by a proven caller whose role may act
/// (ADR-0009, amendments 2026-10-03 and 2026-10-06): the one role check every
/// act passes from a browser or the desktop, over the runtime's gate
/// (<c>xmip_act_admitted_v1</c>) a node's operate listener asks too. It reads
/// as <paramref name="inner"/> reads; each act — pause and resume a scope,
/// pause and resume a Subscription, pause, resume and remove an Event
/// subscription, replay a Message from a Dead Message Queue,
/// retry or dismiss a Message that failed — is refused in words, and nothing
/// reaches <paramref name="inner"/>, unless <paramref name="context"/>'s caller was
/// proven and the role <see cref="RoleAssignment"/> granted them may operate.
/// Every act, taken or refused, is audited as that caller (ADR-0062).
/// </summary>
/// <remarks>
/// The <c>who</c> a caller passes to an act is not read: the act is
/// <paramref name="context"/>'s, a caller the runtime proved and holds,
/// carried to the gate as its handle and never as a name: on a web host
/// what the runtime's authentication gate answered for the client
/// certificate (<c>xmip_caller_proven_v1</c>; ADR-0009, amendment
/// 2026-11-01), wherever the connection came from; the desktop's, the
/// account the runtime read from the operating system
/// (<c>xmip_caller_process_v1</c>). Where nothing was proven, or the handle
/// was released, every act is refused rather than taken on the caller's
/// word.
/// The <c>caller</c> an act names is not read either: an act the gate
/// admitted is handed on for the gate's own caller, so through a listener
/// the node decides again by that caller's own role (ADR-0009, the second
/// amendment of 2026-11-01).
/// </remarks>
/// <param name="inner">The surface read and acted through.</param>
/// <param name="context">Who asks and the role they hold
/// (<see cref="RoleAssignment.For"/>).</param>
/// <param name="audit">Where every act, taken or refused, is recorded.</param>
public sealed class GatedOperator(
    IOperatorSurface inner, RoleContext context, ProgramAudit audit) : IOperatorSurface
{
    /// <summary>The audit action every act through the gate records.</summary>
    public const string AuditAction = "act";

    /// <inheritdoc />
    public string Source => inner.Source;

    /// <inheritdoc />
    public IReadOnlyList<HealthRecord> Health(string scope)
    {
        return inner.Health(scope);
    }

    /// <inheritdoc />
    public MeasurementRecord? Measure(string scope, Counted counted)
    {
        return inner.Measure(scope, counted);
    }

    /// <inheritdoc />
    public Figures Figures(string scope)
    {
        return inner.Figures(scope);
    }

    /// <inheritdoc />
    public string Root()
    {
        return inner.Root();
    }

    /// <inheritdoc />
    public Figures Stage(string stage)
    {
        return inner.Stage(stage);
    }

    /// <inheritdoc />
    public IReadOnlyList<string> Locations(string stage)
    {
        return inner.Locations(stage);
    }

    /// <inheritdoc />
    public Figures MessagePath()
    {
        return inner.MessagePath();
    }

    /// <inheritdoc />
    public ScopeIndex Index()
    {
        return inner.Index();
    }

    /// <inheritdoc />
    public ScopeItem Describe(string scope)
    {
        return inner.Describe(scope);
    }

    /// <inheritdoc />
    public IReadOnlyList<ScopeItem> Children(string scope)
    {
        return inner.Children(scope);
    }

    /// <inheritdoc />
    public TopologySnapshot Topology()
    {
        return inner.Topology();
    }

    /// <inheritdoc />
    public IReadOnlyList<string> NodeScopes()
    {
        return inner.NodeScopes();
    }

    /// <inheritdoc />
    public RunHeader Run()
    {
        return inner.Run();
    }

    /// <inheritdoc />
    public NodeCapability Capability(string node)
    {
        return inner.Capability(node);
    }

    /// <inheritdoc />
    public IAsyncEnumerable<SurfaceChange> WatchAsync(CancellationToken stop = default)
    {
        return inner.WatchAsync(stop);
    }

    /// <inheritdoc />
    public EventSubscriptionList EventSubscriptions()
    {
        return inner.EventSubscriptions();
    }

    /// <inheritdoc />
    public SubscriptionList Subscriptions()
    {
        return inner.Subscriptions();
    }

    /// <inheritdoc />
    public EventSubscriptionOperation Act(
        EventSubscriptionRecord subscription, EventSubscriptionAct act, string who,
        HeldCaller? caller)
    {
        ArgumentNullException.ThrowIfNull(subscription);

        string target = $"Event subscription {subscription.Id} on {subscription.Node}";

        return Admits(
                EventSubscriptionOperation.Word(act), target, out string proven, out string refused)
            ? inner.Act(subscription, act, proven, Holding)
            : EventSubscriptionOperation.Declined(subscription, act, refused);
    }

    /// <inheritdoc />
    public SubscriptionOperation Act(
        SubscriptionRecord subscription, SubscriptionAct act, string who, HeldCaller? caller)
    {
        ArgumentNullException.ThrowIfNull(subscription);

        string target = $"Subscription '{subscription.Name}' on {subscription.Node}";

        return Admits(
                SubscriptionOperation.Word(act), target, out string proven, out string refused)
            ? inner.Act(subscription, act, proven, Holding)
            : SubscriptionOperation.Declined(subscription, act, refused);
    }

    /// <inheritdoc />
    public DeadMessageList DeadMessages()
    {
        return inner.DeadMessages();
    }

    /// <inheritdoc />
    public DeadMessageOperation Act(
        DeadMessageRecord message, DeadMessageAct act, string who, HeldCaller? caller)
    {
        ArgumentNullException.ThrowIfNull(message);

        string target = $"Message {message.Message} in the Dead Message Queue of {message.Node}";

        return Admits(DeadMessageOperation.Word(act), target, out string proven, out string refused)
            ? inner.Act(message, act, proven, Holding)
            : DeadMessageOperation.Declined(message, act, refused);
    }

    /// <inheritdoc />
    public FailedMessageList FailedMessages(string scope, ulong from = 0, uint most = 0)
    {
        return inner.FailedMessages(scope, from, most);
    }

    /// <inheritdoc />
    public MessageOperation Act(
        string scope, string journey, MessageAct act, string who, HeldCaller? caller)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(journey);

        string target = $"the Message of the Journey {journey} sent at {scope}";

        return Admits(MessageOperation.Word(act), target, out string proven, out string refused)
            ? inner.Act(scope, journey, act, proven, Holding)
            : MessageOperation.Declined(scope, journey, act, refused);
    }

    /// <inheritdoc />
    public IReadOnlyList<SecretRecord> Secrets(string node, HeldCaller? caller)
    {
        return inner.Secrets(node, Holding);
    }

    /// <inheritdoc />
    public SecretOperation SetSecret(
        string node, string name, string kind, string value, string who, HeldCaller? caller)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(name);

        string target = $"the secret '{name}' at {(node.Length == 0 ? "the node" : node)}";

        return Admits(SecretOperation.Act, target, out string proven, out string refused)
            ? inner.SetSecret(node, name, kind, value, proven, Holding)
            : SecretOperation.Declined(node, name, refused);
    }

    /// <inheritdoc />
    public string PauseScope(string scope, string who, HeldCaller? caller)
    {
        return Admits("pause", scope, out string proven, out string refused)
            ? inner.PauseScope(scope, proven, Holding)
            : refused;
    }

    /// <inheritdoc />
    public string ResumeScope(string scope, HeldCaller? caller)
    {
        return Admits("resume", scope, out _, out string refused)
            ? inner.ResumeScope(scope, Holding)
            : refused;
    }

    /// <inheritdoc />
    public ScopeOperation Control(
        string scope, ScopeAction action, string who, HeldCaller? caller)
    {
        string word = action == ScopeAction.Pause ? "pause" : "resume";

        return Admits(word, scope, out string proven, out string refused)
            ? inner.Control(scope, action, proven, Holding)
            : new ScopeOperation(scope, action, false, refused);
    }

    // Whom an act the gate admitted is for: the gate's own caller, never one
    // an act names, handed to the inner surface, so through a listener the
    // node decides again by that caller's own role (ADR-0009, amendment
    // 2026-11-01).
    private HeldCaller? Holding => context.Caller;

    // The one check, the runtime's (xmip_act_admitted_v1): a caller the
    // runtime holds, by its handle, and a role that may act. Audited either
    // way, with who — the runtime's name for the caller — what, on what and
    // the role it was judged by.
    private bool Admits(string act, string target, out string proven, out string refused)
    {
        ArgumentNullException.ThrowIfNull(context);

        string? who = context.Who;
        Role role = context.Role;
        proven = who ?? string.Empty;

        // The gate is the runtime's, once: a node's operate listener asks it
        // of every act that reaches it from another machine (ADR-0014,
        // amendment 2026-10-12).
        bool admitted = RuntimeLibrary.Rules.SecurityRoles.Admitted(
            context.Caller, (int)role, act, target, out refused);

        audit.Record(
            AuditAction,
            admitted ? AuditPhase.Execute : AuditPhase.Failure,
            admitted ? AuditSeverity.Information : AuditSeverity.Warning,
            admitted ? $"{who} asked to {act} {target}" : refused,
            new Dictionary<string, string>
            {
                ["act"] = act,
                ["target"] = target,
                ["who"] = who ?? string.Empty,
                ["role"] = role.ToString(),
            });

        return admitted;
    }
}
