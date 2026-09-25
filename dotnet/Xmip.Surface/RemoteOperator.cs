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
/// The wire is JSON, which the estate reserves for memory and the wire; the
/// records are the binding's, so a remote answer has the shape a local one
/// has. A host that cannot be reached is said so in <see cref="Source"/> and
/// reports nothing, as ADR-0052 asks of every surface. The two acts cross the
/// wire as they cross the desktop: by role, once the role gate lands
/// (ADR-0009).
/// </remarks>
public sealed class RemoteOperator : IOperatorSurface, IDisposable
{
    /// <summary>Where a web host maps its surface hub.</summary>
    public const string HubPath = "/surface";

    /// <summary>The message a hub sends when its surface changed.</summary>
    public const string ChangedMessage = "Changed";

    /// <summary>How long a call waits for the host before it is unreachable.</summary>
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);

    private readonly HubConnection connection;

    private readonly List<Channel<SurfaceChange>> watchers = [];

    private readonly Lock gate = new();

    private ScopeIndex? index;

    private ulong told;

    private string? refusal;

    /// <summary>Whether <paramref name="url"/> names a web host: an absolute
    /// http or https address, which is what a hub is reached at.</summary>
    public static bool IsWebHost([NotNullWhen(true)] string? url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out Uri? host)
            && host.Scheme is "http" or "https";
    }

    /// <summary>A surface over the web host at <paramref name="host"/>,
    /// presenting and trusting what <paramref name="tls"/> holds — nothing
    /// and the operating system's anchors where it is not given.</summary>
    public RemoteOperator(Uri host, SurfaceTls? tls = null)
    {
        ArgumentNullException.ThrowIfNull(host);

        Host = host;
        Hub = new Uri(host, HubPath);
        Tls = tls ?? SurfaceTls.None;
        connection = new HubConnectionBuilder()
            .WithUrl(Hub, Guard)
            .WithAutomaticReconnect()
            .Build();
        connection.On<SurfaceChange>(ChangedMessage, Announce);
        connection.Reconnected += _ =>
        {
            Announce(SurfaceChange.Initial(Source));
            return Task.CompletedTask;
        };
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
    /// connects first; a caller that wants to know before asking calls this.</summary>
    public bool Connect()
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
    public string PauseScope(string scope, string who)
    {
        return Ask<string>("Pause", scope, who) ?? Source;
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
            Connect();
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
        lock (gate)
        {
            foreach (Channel<SurfaceChange> watcher in watchers)
            {
                watcher.Writer.TryComplete();
            }
        }

        connection.DisposeAsync().AsTask().GetAwaiter().GetResult();
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
