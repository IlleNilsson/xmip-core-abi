using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Xmip.Abi.Module;
using Xmip.Abi.Operate;

namespace Xmip.Surface;

/// <summary>
/// The surface over a node on another machine, reached at its operate
/// address over gRPC inside Xmip's mutual TLS (ADR-0014, amendment
/// 2026-10-12; ADR-0052, amendment 2026-10-05): the runtime's remote
/// surface, <c>xmip_operate.h</c> section 17, which the command line, the
/// PowerShell module and the GUIs all follow a node through. SignalR stays
/// between a web host and its browser.
/// </summary>
/// <remarks>
/// It reads the node's publication — the text its file holds, read by the
/// one reader (<see cref="PublicationReading"/>) — once per revision, and
/// is told, never asks, when it advances: the runtime's follow holds the
/// node's wait. A lost node is told too: every watch at once, with a
/// <see cref="Source"/> that says the node is unreachable and why, and
/// every answer is empty until it is back — never the publication held from
/// before. The follow tries a lost node again on its own loop (ADR-0052,
/// note of 2026-10-05), in the runtime.
/// </remarks>
/// <remarks>
/// Mutual TLS always: this side presents the certificate its
/// <see cref="SurfaceTls"/> names and takes the node only where its
/// certificate reaches the anchors named with it. An act crosses without a
/// name: the node takes it as the identity this side's certificate proved,
/// admitted by the role the node grants it, and refuses it in words
/// otherwise (ADR-0009). The <c>who</c> an act is given here does not leave
/// this process.
/// </remarks>
public sealed class RemoteOperator : IOperatorSurface, IDisposable
{
    /// <summary>How long a watch's one wait lasts before it looks whether
    /// the watch ended; a change wakes it at once.</summary>
    private static readonly TimeSpan Turn = TimeSpan.FromSeconds(1);

    private readonly RemoteNode? node;

    private readonly List<Channel<SurfaceChange>> watchers = [];

    private readonly Lock gate = new();

    private PublicationReading? held;

    private ulong heldRevision;

    private Thread? following;

    private volatile bool ended;

    /// <summary>What every surface says when <c>--remote</c> or
    /// <c>-Remote</c> names no node; null when it names one, whose address
    /// the runtime judges as it opens it.</summary>
    public static string? Refusal(string? address)
    {
        return string.IsNullOrWhiteSpace(address)
            ? "A remote needs a node's operate address, like node1:7401."
            : null;
    }

    /// <summary>A surface over the node at <paramref name="address"/>,
    /// <c>host:port</c>, presenting and trusting what <paramref name="tls"/>
    /// names.</summary>
    public RemoteOperator(string address, SurfaceTls tls)
    {
        ArgumentNullException.ThrowIfNull(address);
        ArgumentNullException.ThrowIfNull(tls);

        Address = address;
        Reason = "not connected yet";

        if (tls.Pem(out string certificate, out string key, out string anchors) is { } missing)
        {
            Reason = missing;
            return;
        }

        node = RuntimeLibrary.Rules.Remotes.Open(
            address, certificate, key, anchors, out string refused);

        if (node is null)
        {
            Reason = refused;
        }
    }

    /// <summary>The node's operate address.</summary>
    public string Address { get; }

    /// <summary>Whether the node answered when last asked or told.</summary>
    public bool IsConnected { get; private set; }

    /// <summary>Why the node does not answer, when it does not.</summary>
    public string Reason { get; private set; }

    /// <inheritdoc />
    public string Source => IsConnected
        ? $"REMOTE — {Address}"
        : $"REMOTE — {Address} unreachable: {Reason}";

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
    /// its oldest hundred.</remarks>
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
    public async IAsyncEnumerable<SurfaceChange> WatchAsync(
        [EnumeratorCancellation] CancellationToken stop = default)
    {
        Channel<SurfaceChange> channel = Channel.CreateUnbounded<SurfaceChange>(
            new UnboundedChannelOptions { SingleReader = true });

        lock (gate)
        {
            watchers.Add(channel);

            if (node is not null && following is null)
            {
                following = new Thread(Follow) { IsBackground = true, Name = "xmip-remote" };
                following.Start();
            }
        }

        try
        {
            yield return SurfaceChange.Initial(Source);

            while (true)
            {
                bool more;

                try
                {
                    more = await channel.Reader.WaitToReadAsync(stop).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // The normal end of a watch.
                    more = false;
                }

                if (!more)
                {
                    yield break;
                }

                while (channel.Reader.TryRead(out SurfaceChange? change))
                {
                    yield return change;
                }
            }
        }
        finally
        {
            lock (gate)
            {
                watchers.Remove(channel);
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        ended = true;

        lock (gate)
        {
            foreach (Channel<SurfaceChange> watcher in watchers)
            {
                watcher.Writer.TryComplete();
            }
        }

        node?.Dispose();
        following?.Join();
    }

    // The publication, read once per revision the follow told; read on every
    // question where nothing follows. Unreachable, nothing — never what was
    // held from before.
    private PublicationReading Read()
    {
        if (node is null)
        {
            IsConnected = false;
            return PublicationReading.Nothing(Source);
        }

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
            if (held is null || heldRevision != revision)
            {
                held = PublicationReading.Of(text, revision, Source);
                heldRevision = revision;
            }

            return held;
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
    // held goes, it is read here — off whoever renders — and every watch is
    // told.
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
                held = null;
            }

            IsConnected = told.Reachable;
            Reason = told.Reachable ? string.Empty : told.Reason;

            if (told.Reachable)
            {
                _ = Read();
            }

            Announce(new SurfaceChange(
                told.Revision, SurfaceChangeKind.All, DateTimeOffset.UtcNow, Source));
        }
    }

    private void Announce(SurfaceChange change)
    {
        lock (gate)
        {
            foreach (Channel<SurfaceChange> watcher in watchers)
            {
                watcher.Writer.TryWrite(change);
            }
        }
    }
}
