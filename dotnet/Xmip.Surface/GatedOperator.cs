using Xmip.Abi.Operate;

namespace Xmip.Surface;

/// <summary>
/// A surface whose acts are taken only by a proven principal whose role may
/// act (ADR-0009, amendment 2026-10-03): the one role check every act that
/// reaches a host from elsewhere passes. It reads as <paramref name="inner"/>
/// reads; each act — pause and resume a scope, pause and resume a
/// Subscription, pause, resume and remove an Event subscription, replay a
/// Message from a Dead Message Queue, retry or dismiss a Journey that
/// failed — is refused
/// in words, and nothing reaches <paramref name="inner"/>, unless
/// <paramref name="who"/> was proven and <paramref name="role"/> may operate.
/// Every act, taken or refused, is audited (ADR-0062).
/// </summary>
/// <remarks>
/// The <c>who</c> a caller passes to an act is not read: the act is
/// <paramref name="who"/>'s, the identity the connection proved, by
/// <see cref="Proven"/>: a client certificate's subject, else, on loopback,
/// the operating system user the host runs as. Where nothing was proven,
/// <paramref name="who"/> is null and every act is refused rather than taken
/// on the caller's word.
/// </remarks>
/// <param name="inner">The surface read and acted through.</param>
/// <param name="role">The role the host runs as (<see cref="RoleContext.Assigned"/>).</param>
/// <param name="who">The proven identity asking, or null.</param>
/// <param name="audit">Where every act, taken or refused, is recorded.</param>
public sealed class GatedOperator(
    IOperatorSurface inner, Role role, string? who, ProgramAudit audit) : IOperatorSurface
{
    /// <summary>The audit action every act through the gate records.</summary>
    public const string AuditAction = "act";

    /// <summary>
    /// The operating system user this process runs as: <c>DOMAIN\user</c> on
    /// Windows, the user name elsewhere.
    /// </summary>
    public static string HostUser => OperatingSystem.IsWindows()
        ? $"{Environment.UserDomainName}\\{Environment.UserName}"
        : Environment.UserName;

    /// <summary>
    /// Who a connection proved, the one rule (ADR-0009, amendment
    /// 2026-10-03): the subject of the client certificate it presented; else,
    /// where it came over this machine's loopback, the operating system user
    /// the host runs as — only someone logged on to the machine reaches
    /// loopback (the owner, 2026-10-03); else no one, and no act is taken.
    /// </summary>
    public static string? Proven(string? certificateSubject, bool loopback)
    {
        return !string.IsNullOrWhiteSpace(certificateSubject)
            ? certificateSubject
            : loopback ? HostUser : null;
    }

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
        EventSubscriptionRecord subscription, EventSubscriptionAct act, string who)
    {
        ArgumentNullException.ThrowIfNull(subscription);

        string target = $"Event subscription {subscription.Id} on {subscription.Node}";

        return Admits(
                EventSubscriptionOperation.Word(act), target, out string proven, out string refused)
            ? inner.Act(subscription, act, proven)
            : EventSubscriptionOperation.Declined(subscription, act, refused);
    }

    /// <inheritdoc />
    public SubscriptionOperation Act(
        SubscriptionRecord subscription, SubscriptionAct act, string who)
    {
        ArgumentNullException.ThrowIfNull(subscription);

        string target = $"Subscription '{subscription.Name}' on {subscription.Node}";

        return Admits(
                SubscriptionOperation.Word(act), target, out string proven, out string refused)
            ? inner.Act(subscription, act, proven)
            : SubscriptionOperation.Declined(subscription, act, refused);
    }

    /// <inheritdoc />
    public DeadMessageList DeadMessages()
    {
        return inner.DeadMessages();
    }

    /// <inheritdoc />
    public DeadMessageOperation Act(DeadMessageRecord message, DeadMessageAct act, string who)
    {
        ArgumentNullException.ThrowIfNull(message);

        string target = $"Message {message.Message} in the Dead Message Queue of {message.Node}";

        return Admits(DeadMessageOperation.Word(act), target, out string proven, out string refused)
            ? inner.Act(message, act, proven)
            : DeadMessageOperation.Declined(message, act, refused);
    }

    /// <inheritdoc />
    public JourneyOperation Act(string scope, string journey, JourneyAct act, string who)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(journey);

        string target = $"the Journey {journey} sent at {scope}";

        return Admits(JourneyOperation.Word(act), target, out string proven, out string refused)
            ? inner.Act(scope, journey, act, proven)
            : JourneyOperation.Declined(scope, journey, act, refused);
    }

    /// <inheritdoc />
    public string PauseScope(string scope, string who)
    {
        return Admits("pause", scope, out string proven, out string refused)
            ? inner.PauseScope(scope, proven)
            : refused;
    }

    /// <inheritdoc />
    public string ResumeScope(string scope)
    {
        return Admits("resume", scope, out _, out string refused)
            ? inner.ResumeScope(scope)
            : refused;
    }

    /// <inheritdoc />
    public ScopeOperation Control(string scope, ScopeAction action, string who)
    {
        string word = action == ScopeAction.Pause ? "pause" : "resume";

        return Admits(word, scope, out string proven, out string refused)
            ? inner.Control(scope, action, proven)
            : new ScopeOperation(scope, action, false, refused);
    }

    // The one check: a proven principal, and a role that may act. Audited
    // either way, with who, what, on what and the role it was judged by.
    private bool Admits(string act, string target, out string proven, out string refused)
    {
        proven = who ?? string.Empty;
        refused = who is null
            ? $"REFUSED. Nothing proved who asks to {act} {target}; an act is taken from "
              + "the identity the connection proved — a client certificate, or the host's "
              + "own user on loopback — never from what the caller says (ADR-0009)"
            : role.MayOperate()
                ? string.Empty
                : $"REFUSED. {who} may not {act} {target}: this host runs as {role}, "
                  + $"which {role.Describe()} (ADR-0009)";
        bool admitted = refused.Length == 0;

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
