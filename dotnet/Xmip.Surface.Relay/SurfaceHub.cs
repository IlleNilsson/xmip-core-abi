using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Xmip.Abi.Operate;

namespace Xmip.Surface.Relay;

/// <summary>
/// The host's surface, served: each method answers what the host's own
/// <see cref="IOperatorSurface"/> answers, so a <see cref="RemoteOperator"/>
/// on another machine reads the same records a screen on this one reads.
/// Changes are not asked for here; <see cref="SurfaceRelay"/> pushes them.
/// </summary>
/// <remarks>
/// Every act goes through <see cref="GatedOperator"/>, the one role check
/// (ADR-0009, amendments 2026-10-03 and 2026-10-06): it is taken only where
/// the role the host's <see cref="RoleAssignment"/> grants the caller may
/// act, and as the identity the connection proved — the
/// subject of the client certificate the TLS handshake checked against the
/// host's anchors (<see cref="SurfaceBinding.UseXmipTls"/>). No act takes a
/// name from the caller. Over loopback without a certificate the act is the
/// operating system user the host runs as, since only someone logged on to
/// the machine reaches loopback; from anywhere else without one, it is
/// refused. Every act, taken or refused, is audited (ADR-0062).
/// </remarks>
public sealed class SurfaceHub(
    IOperatorSurface surface, RoleAssignment roles, ProgramAudit audit)
    : Hub
{
    /// <summary>Where a web host maps this hub.</summary>
    public const string Path = RemoteOperator.HubPath;

    // What the gate is handed as the caller's word, which it does not read.
    private const string Unread = "";

    /// <summary>Where the host's records come from.</summary>
    public string Source()
    {
        return surface.Source;
    }

    /// <summary>Health at and beneath a scope, worst first.</summary>
    public IReadOnlyList<HealthRecord> Health(string scope)
    {
        return surface.Health(scope);
    }

    /// <summary>One kind of count, summed over the scope.</summary>
    public MeasurementRecord? Measure(string scope, Counted counted)
    {
        return surface.Measure(scope, counted);
    }

    /// <summary>The configured and observed communication topology.</summary>
    public TopologySnapshot Topology()
    {
        return surface.Topology();
    }

    /// <summary>What the run behind the surface was started with.</summary>
    public RunHeader Run()
    {
        return surface.Run();
    }

    /// <summary>The scope the host's publisher publishes at, where a drill
    /// starts.</summary>
    public string Root()
    {
        return surface.Root();
    }

    /// <summary>The Subscriptions the host's surface lists.</summary>
    public SubscriptionList Subscriptions()
    {
        return surface.Subscriptions();
    }

    /// <summary>Pause or resume one Subscription, as the proven caller, where
    /// the caller's role may act. There is no remove.</summary>
    public SubscriptionOperation ActOnSubscription(
        SubscriptionRecord subscription, SubscriptionAct act)
    {
        return Acting().Act(subscription, act, Unread);
    }

    /// <summary>The Event subscriptions the host's surface lists.</summary>
    public EventSubscriptionList EventSubscriptions()
    {
        return surface.EventSubscriptions();
    }

    /// <summary>Pause, resume or remove one Event subscription, as the proven
    /// caller, where the caller's role may act.</summary>
    public EventSubscriptionOperation ActOnEventSubscription(
        EventSubscriptionRecord subscription, EventSubscriptionAct act)
    {
        return Acting().Act(subscription, act, Unread);
    }

    /// <summary>What the Dead Message Queues the host's surface lists
    /// keep.</summary>
    public DeadMessageList DeadMessages()
    {
        return surface.DeadMessages();
    }

    /// <summary>Replay one Message from a Dead Message Queue, as the proven
    /// caller, where the caller's role may act.</summary>
    public DeadMessageOperation ActOnDeadMessage(DeadMessageRecord message, DeadMessageAct act)
    {
        return Acting().Act(message, act, Unread);
    }

    /// <summary>The Journeys that failed at the Send Ports at or beneath
    /// <paramref name="scope"/>, as the host's surface lists them.</summary>
    public FailedJourneyList FailedJourneys(string scope, ulong from, uint most)
    {
        return surface.FailedJourneys(scope, from, most);
    }

    /// <summary>Retry or Dismiss a Journey that failed, sent by the node at
    /// or above <paramref name="scope"/>, as the proven caller, where the
    /// caller's role may act.</summary>
    public JourneyOperation ActOnJourney(string scope, string journey, JourneyAct act)
    {
        return Acting().Act(scope, journey, act, Unread);
    }

    /// <summary>Pause or resume everything at and beneath a scope, as the
    /// proven caller, where the caller's role may act, and say what came of it
    /// as the host's surface says it.</summary>
    public ScopeOperation ActOnScope(string scope, ScopeAction action)
    {
        return Acting().Control(scope, action, Unread);
    }

    /// <summary>The host's surface behind the one role check, for the
    /// identity this connection proved, by
    /// <see cref="ProvenCaller.Of(ConnectionInfo?)"/>.</summary>
    private GatedOperator Acting()
    {
        return new GatedOperator(
            surface, roles.For(ProvenCaller.Of(Context.GetHttpContext()?.Connection)), audit);
    }
}
