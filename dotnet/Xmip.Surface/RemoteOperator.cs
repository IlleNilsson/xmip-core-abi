using System.Diagnostics.CodeAnalysis;
using System.Net.Security;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Channels;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.AspNetCore.SignalR.Client;
using Xmip.Abi.Operate;

namespace Xmip.Surface;

/// <summary>
/// The surface over a web host on another machine: a SignalR connection to
/// the host's surface hub, which serves whatever surface that host reads. A
/// surface is told, it does not ask (ADR-0052, amendment 2026-09-15): the hub
/// pushes a <see cref="SurfaceChange"/> whenever its own feed fires, and this
/// side rereads what it needs, as every consumer of the feed does. The address
/// is given by whoever chose this surface (ADR-0052 clause 3).
/// </summary>
/// <remarks>
/// The connection is TLS: this side presents the certificate its
/// <see cref="SurfaceTls"/> holds and takes the host only when the host's
/// certificate reaches its anchors (ADR-0063 clause 1). Plain HTTP is
/// refused unless the host is this machine, the one exception, and the
/// refusal is the <see cref="Reason"/>.
/// </remarks>
/// <remarks>
/// A lost host is told too: when the connection closes, every watch is told
/// at once, with a <see cref="Source"/> that says the host is unreachable and
/// why, and every answer is empty until it is back — never the publication
/// held from before (ADR-0052: a host that cannot be reached reports
/// nothing). While anything watches, the connection is tried again on
/// <see cref="Retry"/>'s schedule, whether it was lost or never made, and a
/// watch is told the moment it is made again.
/// </remarks>
/// <remarks>
/// The wire is JSON, which the estate reserves for memory and the wire; the
/// records are the binding's, so a remote answer has the shape a local one
/// has. A host that cannot be reached is said so in <see cref="Source"/> and
/// reports nothing, as ADR-0052 asks of every surface. An act crosses the
/// wire without a name: the host takes it as the identity this side's
/// certificate proved, where the host's role may act, and refuses it in
/// words otherwise (ADR-0009, amendment 2026-10-03). The <c>who</c> an act
/// is given here does not leave this process.
/// </remarks>
public sealed class RemoteOperator : IOperatorSurface, IDisposable
{
    /// <summary>Where a web host maps its surface hub.</summary>
    public const string HubPath = "/surface";

    /// <summary>The message a hub sends when its surface changed.</summary>
    public const string ChangedMessage = "Changed";

    /// <summary>How long a call waits for the host before it is unreachable.</summary>
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);

    /// <summary>How long a watch waits before each try to reach a host it
    /// lost or never reached, the last repeated for as long as it watches:
    /// SignalR's own reconnect schedule, which here never gives up.</summary>
    public static readonly IReadOnlyList<TimeSpan> Retry =
    [
        TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30),
    ];

    private readonly IReadOnlyList<TimeSpan> retry;

    private readonly CancellationTokenSource ending = new();

    private readonly HubConnection connection;

    private readonly List<Channel<SurfaceChange>> watchers = [];

    private readonly Lock gate = new();

    private ScopeIndex? index;

    private ulong told;

    private string? refusal;

    // 1 while a watch is trying to reach the host again; an answer asked
    // meanwhile says unreachable at once rather than wait on a second try.
    private int recovering;

    /// <summary>Whether <paramref name="url"/> names a web host: an absolute
    /// http or https address, which is what a hub is reached at.</summary>
    public static bool IsWebHost([NotNullWhen(true)] string? url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out Uri? host)
            && host.Scheme is "http" or "https";
    }

    /// <summary>What every surface says when a remote it was told to follow
    /// is no web host, <paramref name="url"/> included where one was given;
    /// null when it is one. <c>--remote</c> and <c>-Remote</c> both say
    /// this.</summary>
    public static string? Refusal(string? url)
    {
        const string Needs = "A remote needs a web host, like https://host:5443";

        return IsWebHost(url)
            ? null
            : string.IsNullOrWhiteSpace(url) ? $"{Needs}." : $"{Needs}; not {url}.";
    }

    /// <summary>A surface over the web host at <paramref name="host"/>,
    /// presenting and trusting what <paramref name="tls"/> holds — nothing
    /// and the operating system's anchors where it is not given.</summary>
    public RemoteOperator(Uri host, SurfaceTls? tls = null)
        : this(host, tls, Retry)
    {
    }

    /// <summary>The same, trying a lost host again on <paramref name="retry"/>'s
    /// schedule: what a test shortens.</summary>
    internal RemoteOperator(Uri host, SurfaceTls? tls, IReadOnlyList<TimeSpan> retry)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(retry);

        Host = host;
        Hub = new Uri(host, HubPath);
        Tls = tls ?? SurfaceTls.None;
        this.retry = retry.Count > 0 ? retry : Retry;
        connection = new HubConnectionBuilder()
            .WithUrl(Hub, Guard)
            .Build();
        connection.On<SurfaceChange>(ChangedMessage, Announce);
        connection.Closed += Lost;
    }

    /// <summary>The web host this surface follows.</summary>
    public Uri Host { get; }

    /// <summary>The hub on that host.</summary>
    public Uri Hub { get; }

    /// <summary>What this side presents and trusts.</summary>
    public SurfaceTls Tls { get; }

    /// <summary>Whether the host answers right now.</summary>
    public bool IsConnected => connection.State == HubConnectionState.Connected;

    /// <summary>Why the host does not answer, when it does not.</summary>
    public string Reason { get; private set; } = "not connected yet";

    /// <inheritdoc />
    public string Source => (IsConnected, Reason.Length) switch
    {
        (false, _) => $"REMOTE — {Host} unreachable: {Reason}",
        (true, 0) => $"REMOTE — {Host}",
        (true, _) => $"REMOTE — {Host} answered nothing: {Reason}",
    };

    /// <summary>Connect, or say why not in <see cref="Reason"/>. Every answer
    /// connects first; a caller that wants to know before asking calls this.
    /// While a watch is trying the host again, it says so without a second
    /// try of its own.</summary>
    public bool Connect()
    {
        return IsConnected || (Volatile.Read(ref recovering) == 0 && Reach());
    }

    // One try at the host, one at a time.
    private bool Reach()
    {
        if (IsConnected)
        {
            return true;
        }

        lock (gate)
        {
            if (IsConnected)
            {
                return true;
            }

            if (!SurfaceTls.Permits(Host, out string plain))
            {
                Reason = plain;
                return false;
            }

            if (connection.State != HubConnectionState.Disconnected)
            {
                Reason = connection.State.ToString().ToLowerInvariant();
                return false;
            }

            try
            {
                refusal = null;
                using CancellationTokenSource patience = new(Patience);
                connection.StartAsync(patience.Token).GetAwaiter().GetResult();
                Reason = string.Empty;
                return true;
            }
            catch (Exception failure)
            {
                // A certificate this side refused says which and why, not the
                // platform's "the SSL connection could not be established".
                Reason = refusal is { } refused ? $"REFUSED. {refused}" : failure.Message;
                return false;
            }
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<HealthRecord> Health(string scope)
    {
        return Index().Health(scope);
    }

    /// <summary>
    /// The host's publication as its tree, fetched once per notice and kept
    /// until the next (ADR-0052, amendment 2026-09-15): one trip per
    /// publication, not one per row.
    /// </summary>
    public ScopeIndex Index()
    {
        // Unreachable, the answer is nothing, said so; what was held from
        // before the host was lost is never handed out as if current.
        if (!Connect())
        {
            return ScopeIndex.Build([], [], 0, Source);
        }

        ulong revision = Volatile.Read(ref told);

        lock (gate)
        {
            if (index is { } held && revision != 0 && held.Revision == revision)
            {
                return held;
            }
        }

        ScopeIndex built = ScopeIndex.Build(
            Ask<HealthRecord[]>("Health", ScopeTree.Root) ?? [], [], revision, Source);

        lock (gate)
        {
            index = built;
        }

        return built;
    }

    /// <inheritdoc />
    public MeasurementRecord? Measure(string scope, Counted counted)
    {
        return Ask<MeasurementRecord?>("Measure", scope, counted);
    }

    /// <inheritdoc />
    public TopologySnapshot Topology()
    {
        return Ask<TopologySnapshot>("Topology") ?? TopologySnapshot.Empty(Source);
    }

    /// <inheritdoc />
    public RunHeader Run()
    {
        return Ask<RunHeader>("Run") ?? RunHeader.None;
    }

    /// <inheritdoc />
    public string Root()
    {
        return Ask<string>("Root") ?? ScopeTree.Root;
    }

    /// <inheritdoc />
    public EventSubscriptionList EventSubscriptions()
    {
        return Ask<EventSubscriptionList>("EventSubscriptions") ?? EventSubscriptionList.Empty;
    }

    /// <inheritdoc />
    public EventSubscriptionOperation Act(
        EventSubscriptionRecord subscription, EventSubscriptionAct act, string who)
    {
        return Ask<EventSubscriptionOperation>("ActOnEventSubscription", subscription, act)
            ?? EventSubscriptionOperation.Declined(subscription, act, Source);
    }

    /// <inheritdoc />
    public SubscriptionList Subscriptions()
    {
        return Ask<SubscriptionList>("Subscriptions") ?? SubscriptionList.Empty;
    }

    /// <inheritdoc />
    public SubscriptionOperation Act(
        SubscriptionRecord subscription, SubscriptionAct act, string who)
    {
        return Ask<SubscriptionOperation>("ActOnSubscription", subscription, act)
            ?? SubscriptionOperation.Declined(subscription, act, Source);
    }

    /// <inheritdoc />
    public DeadMessageList DeadMessages()
    {
        return Ask<DeadMessageList>("DeadMessages") ?? DeadMessageList.Empty;
    }

    /// <inheritdoc />
    public DeadMessageOperation Act(DeadMessageRecord message, DeadMessageAct act, string who)
    {
        return Ask<DeadMessageOperation>("ActOnDeadMessage", message, act)
            ?? DeadMessageOperation.Declined(message, act, Source);
    }

    /// <inheritdoc />
    public FailedJourneyList FailedJourneys(string scope, ulong from = 0, uint most = 0)
    {
        return Ask<FailedJourneyList>("FailedJourneys", scope, from, most)
            ?? FailedJourneyList.Empty;
    }

    /// <inheritdoc />
    public JourneyOperation Act(string scope, string journey, JourneyAct act, string who)
    {
        return Ask<JourneyOperation>("ActOnJourney", scope, journey, act)
            ?? JourneyOperation.Declined(scope, journey, act, Source);
    }

    /// <inheritdoc />
    public string PauseScope(string scope, string who)
    {
        return Ask<string>("Pause", scope) ?? Source;
    }

    /// <inheritdoc />
    public string ResumeScope(string scope)
    {
        return Ask<string>("Resume", scope) ?? Source;
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
        }

        try
        {
            if (!Connect())
            {
                Recover();
            }

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
        ending.Cancel();

        lock (gate)
        {
            foreach (Channel<SurfaceChange> watcher in watchers)
            {
                watcher.Writer.TryComplete();
            }
        }

        connection.DisposeAsync().AsTask().GetAwaiter().GetResult();
        ending.Dispose();
    }

    /// <summary>
    /// Both of the connection's paths, the HTTP negotiation and the web
    /// socket, present this side's certificate and check the host's by
    /// <see cref="SurfaceTls.Accepts"/>.
    /// </summary>
    private void Guard(HttpConnectionOptions options)
    {
        if (Tls.Certificate is { } presented)
        {
            options.ClientCertificates = [presented];
        }

        options.HttpMessageHandlerFactory = handler =>
        {
            if (handler is HttpClientHandler http)
            {
                http.ServerCertificateCustomValidationCallback =
                    (_, certificate, chain, errors) => Checked(certificate, chain, errors);
            }

            return handler;
        };
        options.WebSocketConfiguration = socket => socket.RemoteCertificateValidationCallback =
            (_, certificate, chain, errors) => Checked(certificate, chain, errors);
    }

    private bool Checked(
        X509Certificate? certificate,
        X509Chain? chain,
        SslPolicyErrors errors)
    {
        if (Tls.Accepts(
            certificate, chain, errors, SurfaceTls.ServerAuthentication, out string reason))
        {
            return true;
        }

        refusal = reason;
        return false;
    }

    // The connection closed: every watch is told now, with the host said
    // unreachable and why, and the host is tried again while anything
    // watches. A close this side asked for is no loss.
    private Task Lost(Exception? failure)
    {
        if (ending.IsCancellationRequested)
        {
            return Task.CompletedTask;
        }

        Reason = failure is null ? "the host closed the connection" : $"lost: {failure.Message}";
        Announce(SurfaceChange.Initial(Source));
        Recover();
        return Task.CompletedTask;
    }

    // Try the host again on the retry schedule, one recovery at a time, for
    // as long as anything watches. Nothing can tell this side that a host
    // which is not there is back, so this is the one place a surface asks
    // rather than is told; the moment the host answers, every watch is told.
    private void Recover()
    {
        if (Interlocked.Exchange(ref recovering, 1) == 1)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                for (int attempt = 0; Watched(); attempt++)
                {
                    await Task.Delay(retry[Math.Min(attempt, retry.Count - 1)], ending.Token)
                        .ConfigureAwait(false);

                    if (Reach())
                    {
                        Interlocked.Exchange(ref recovering, 0);
                        Announce(SurfaceChange.Initial(Source));
                        return;
                    }
                }
            }
            catch (Exception stopped) when (stopped is OperationCanceledException
                or ObjectDisposedException)
            {
                // Disposed: nothing watches any more.
                Interlocked.Exchange(ref recovering, 0);
                return;
            }

            Interlocked.Exchange(ref recovering, 0);

            // A watch begun as this one gave up found it still running.
            if (Watched() && !IsConnected)
            {
                Recover();
            }
        });
    }

    private bool Watched()
    {
        lock (gate)
        {
            return watchers.Count > 0;
        }
    }

    private void Announce(SurfaceChange change)
    {
        Volatile.Write(ref told, change.Revision);

        lock (gate)
        {
            foreach (Channel<SurfaceChange> watcher in watchers)
            {
                watcher.Writer.TryWrite(change);
            }
        }
    }

    private T? Ask<T>(string method, params object?[] arguments)
    {
        if (!Connect())
        {
            return default;
        }

        try
        {
            using CancellationTokenSource patience = new(Patience);

            return connection.InvokeCoreAsync<T>(method, arguments, patience.Token)
                .GetAwaiter()
                .GetResult();
        }
        catch (Exception failure)
        {
            Reason = failure.Message;
            return default;
        }
    }
}
