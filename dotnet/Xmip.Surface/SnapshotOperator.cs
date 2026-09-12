using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.Extensions.Configuration;
using Xmip.Abi.Operate;

namespace Xmip.Surface;

/// <summary>
/// The surface over a published snapshot: what a node — or the Playground,
/// after every tick — wrote to a file, read fresh on every query so each
/// render reflects the latest round. The path is given by whoever chose this
/// surface (ADR-0052 clause 3); nothing here guesses at a temp directory, and
/// a path with no file behind it says so in <see cref="Source"/> rather than
/// reporting an empty estate as if it were one.
/// </summary>
/// <remarks>
/// The file is TOML — on disk the estate is TOML, and JSON is reserved for
/// memory and the wire — read with the same reader every surface uses. A
/// read-only surface: it reports what was published and never acts on it, so
/// <see cref="PauseScope"/> and <see cref="ResumeScope"/> decline. ADR-0027,
/// ADR-0028.
/// </remarks>
public sealed class SnapshotOperator(string path) : IOperatorSurface
{
    private readonly Lock gate = new();

    private Snapshot? cached;

    private DateTime cachedWrite;

    private long cachedLength;

    /// <summary>The snapshot file this surface reads.</summary>
    public string Path { get; } = path;

    /// <summary>Whether there is a file at <see cref="Path"/> right now.</summary>
    public bool Exists => File.Exists(Path);

    /// <inheritdoc />
    public string Source => Exists ? $"SNAPSHOT — {Path}" : $"SNAPSHOT — no file at {Path}";

    /// <inheritdoc />
    public IReadOnlyList<HealthRecord> Health(string scope)
    {
        return ScopeTree.WorstFirst(
            Read().Records.Where(record => ScopeTree.Beneath(record.Scope, scope)));
    }

    /// <inheritdoc />
    public TopologySnapshot Topology()
    {
        return Read().Topology;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<SurfaceChange> WatchAsync(
        [EnumeratorCancellation] CancellationToken stop = default)
    {
        string fullPath = System.IO.Path.GetFullPath(Path);
        string? directory = System.IO.Path.GetDirectoryName(fullPath);

        if (directory is null || !Directory.Exists(directory))
        {
            yield return SurfaceChange.Initial(Source);
            yield break;
        }

        Channel<bool> changed = Channel.CreateBounded<bool>(
            new BoundedChannelOptions(1)
            {
                FullMode = BoundedChannelFullMode.DropWrite,
                SingleReader = true,
                SingleWriter = false,
            });

        using FileSystemWatcher watcher = new(directory, System.IO.Path.GetFileName(fullPath))
        {
            NotifyFilter = NotifyFilters.FileName
                | NotifyFilters.LastWrite
                | NotifyFilters.Size
                | NotifyFilters.CreationTime,
        };

        void Signal(object sender, FileSystemEventArgs arguments)
        {
            changed.Writer.TryWrite(true);
        }

        void Renamed(object sender, RenamedEventArgs arguments)
        {
            changed.Writer.TryWrite(true);
        }

        FileSystemEventHandler signal = Signal;
        RenamedEventHandler renamed = Renamed;
        watcher.Changed += signal;
        watcher.Created += signal;
        watcher.Deleted += signal;
        watcher.Renamed += renamed;
        watcher.EnableRaisingEvents = true;

        // Attach before announcing the current view. A replacement racing the
        // first read is then either already visible or queued as a change.
        yield return SurfaceChange.Initial(Source);
        ulong revision = 0;

        try
        {
            await foreach (bool notice in changed.Reader.ReadAllAsync(stop).ConfigureAwait(false))
            {
                _ = notice;

                // Publishers replace atomically. A short yield also coalesces
                // the several filesystem notifications one replacement can emit.
                await Task.Delay(TimeSpan.FromMilliseconds(25), stop).ConfigureAwait(false);
                while (changed.Reader.TryRead(out _))
                {
                }

                yield return new SurfaceChange(
                    ++revision, SurfaceChangeKind.All, DateTimeOffset.UtcNow, Source);
            }
        }
        finally
        {
            changed.Writer.TryComplete();
        }
    }

    /// <inheritdoc />
    public MeasurementRecord? Measure(string scope, Counted counted)
    {
        IReadOnlyList<CountRecord> matching =
        [
            .. Read().Counts
            .Where(count => count.Counted == counted && ScopeTree.Beneath(count.Scope, scope))
        ];

        if (matching.Count == 0)
        {
            return null;
        }

        ulong value = matching.Aggregate(0UL, (sum, count) => sum + count.Value);

        DateTimeOffset now = DateTimeOffset.UtcNow;

        return new MeasurementRecord(scope, counted, value, now.AddMinutes(-1), now, now);
    }

    /// <inheritdoc />
    public string PauseScope(string scope, string who)
    {
        return "a snapshot is a record of what was published and cannot be paused";
    }

    /// <inheritdoc />
    public string ResumeScope(string scope)
    {
        return "a snapshot is a record of what was published and cannot be resumed";
    }

    private sealed record Snapshot(
        IReadOnlyList<HealthRecord> Records,
        IReadOnlyList<CountRecord> Counts,
        TopologySnapshot Topology);

    private sealed record CountRecord(string Scope, Counted Counted, ulong Value);

    /// <summary>Parse the file each call. A missing or half-written file (a
    /// publisher writes atomically, but it may not have ticked yet) reads as an
    /// empty snapshot rather than an error, so the page shows nothing rather
    /// than breaking, and <see cref="Source"/> says why.</summary>
    private Snapshot Read()
    {
        // Parsed once per publication, not once per query: a board asks
        // several times per render, every two seconds, and a Playground
        // snapshot is fourteen thousand records. Parsing it four times a
        // refresh starved the page's own clicks (found 2026-09-11). The
        // file's write time and length say whether anything changed.
        lock (gate)
        {
            FileInfo file = new(Path);

            if (cached is not null
                && file.Exists
                && file.LastWriteTimeUtc == cachedWrite
                && file.Length == cachedLength)
            {
                return cached;
            }

            Snapshot fresh = Parse(file);
            cached = fresh;
            cachedWrite = file.Exists ? file.LastWriteTimeUtc : default;
            cachedLength = file.Exists ? file.Length : 0;

            return fresh;
        }
    }

    private Snapshot Parse(FileInfo file)
    {
        try
        {
            if (!file.Exists)
            {
                return new Snapshot([], [], TopologySnapshot.Empty(Source));
            }

            IConfigurationRoot document = TomlDocument.Read(Path);
            string node = document["node"] ?? ScopeTree.Root;

            List<HealthRecord> records = [];

            foreach (IConfigurationSection row in document.GetSection("records").GetChildren())
            {
                records.Add(new HealthRecord(
                    row["scope"] ?? string.Empty,
                    ParseState(row["state"]),
                    ParseByte(row["severity"]),
                    row["evidence"] ?? string.Empty,
                    ParseObserved(row["observed_unix_nanos"])));
            }

            List<CountRecord> counts = [];

            foreach (IConfigurationSection row in document.GetSection("counts").GetChildren())
            {
                counts.Add(new CountRecord(
                    node, ParseCounted(row["counted"]), ParseUlong(row["value"])));
            }

            List<TopologyNode> nodes = [];

            foreach (IConfigurationSection row in document.GetSection("topology:nodes").GetChildren())
            {
                nodes.Add(new TopologyNode(
                    row["id"] ?? string.Empty,
                    EmptyAsNull(row["parent"]),
                    row["label"] ?? row["id"] ?? string.Empty,
                    ParseNodeKind(row["kind"]),
                    row["scope"] ?? string.Empty,
                    ParseState(row["state"]),
                    ParseOrigin(row["origin"]),
                    ParseDouble(row["load"]),
                    ParseDouble(row["activity"]),
                    row["evidence"] ?? string.Empty));
            }

            List<CommunicationLink> links = [];

            foreach (IConfigurationSection row in document.GetSection("topology:links").GetChildren())
            {
                links.Add(new CommunicationLink(
                    row["id"] ?? string.Empty,
                    row["from"] ?? string.Empty,
                    row["to"] ?? string.Empty,
                    ParsePattern(row["pattern"]),
                    ParseOrigin(row["origin"]),
                    row["protocol"] ?? string.Empty,
                    ParseState(row["state"]),
                    ParseUlong(row["volume"]),
                    ParseDouble(row["rate"]),
                    ParseDouble(row["latency_ms"]),
                    ParseDouble(row["progress"]),
                    ParseUint(row["attempts"]),
                    row["evidence"] ?? string.Empty));
            }

            DateTimeOffset observed = ParseObserved(document["topology:observed_unix_nanos"]);
            string topologySource = document["topology:source"] ?? document["source"] ?? Source;

            return new Snapshot(
                records,
                counts,
                new TopologySnapshot(nodes, links, observed, topologySource));
        }
        catch (Exception exception)
            when (exception is IOException or FormatException or InvalidOperationException)
        {
            return new Snapshot([], [], TopologySnapshot.Empty(Source));
        }
    }

    private static string? EmptyAsNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static TopologyNodeKind ParseNodeKind(string? kind)
    {
        return kind switch
        {
            "computer" => TopologyNodeKind.Computer,
            "server" => TopologyNodeKind.Server,
            "virtual-machine" => TopologyNodeKind.VirtualMachine,
            "gateway" => TopologyNodeKind.Gateway,
            "appliance" => TopologyNodeKind.Appliance,
            "service" => TopologyNodeKind.Service,
            "process" => TopologyNodeKind.Process,
            "interface" => TopologyNodeKind.Interface,
            "port" => TopologyNodeKind.Port,
            "protocol" => TopologyNodeKind.Protocol,
            "location" => TopologyNodeKind.Location,
            _ => TopologyNodeKind.Computer,
        };
    }

    private static TopologyOrigin ParseOrigin(string? origin)
    {
        return origin switch
        {
            "configured" => TopologyOrigin.Configured,
            "observed" => TopologyOrigin.Observed,
            _ => TopologyOrigin.Both,
        };
    }

    private static CommunicationPattern ParsePattern(string? pattern)
    {
        return pattern switch
        {
            "request-response" => CommunicationPattern.RequestResponse,
            "send-receive" => CommunicationPattern.SendReceive,
            "publish-consume" => CommunicationPattern.PublishConsume,
            "streaming" => CommunicationPattern.Streaming,
            "fire-and-forget" => CommunicationPattern.FireAndForget,
            "session" => CommunicationPattern.Session,
            "retry" => CommunicationPattern.Retry,
            _ => CommunicationPattern.SendReceive,
        };
    }

    private static double ParseDouble(string? value)
    {
        return double.TryParse(
            value,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out double parsed)
            ? parsed
            : 0D;
    }

    private static uint ParseUint(string? value)
    {
        return uint.TryParse(value, out uint parsed) ? parsed : 0U;
    }

    private static HealthState ParseState(string? state)
    {
        return state switch
        {
            "fine" => HealthState.Fine,
            "paused" => HealthState.Paused,
            "working" => HealthState.Working,
            "stressed" => HealthState.Stressed,
            "exhausted" => HealthState.Exhausted,
            "done" => HealthState.Done,
            "holding" => HealthState.Holding,
            _ => HealthState.Stressed,
        };
    }

    private static Counted ParseCounted(string? counted)
    {
        return counted switch
        {
            "streams" => Counted.Streams,
            "messages" => Counted.Messages,
            "journeys" => Counted.Journeys,
            "bytes" => Counted.Bytes,
            "retrying" => Counted.Retrying,
            "failed" => Counted.Failed,
            _ => Counted.Streams,
        };
    }

    private static byte ParseByte(string? value)
    {
        return byte.TryParse(value, out byte parsed) ? parsed : (byte)0;
    }

    private static ulong ParseUlong(string? value)
    {
        return ulong.TryParse(value, out ulong parsed) ? parsed : 0UL;
    }

    private static DateTimeOffset ParseObserved(string? nanos)
    {
        return long.TryParse(nanos, out long value)
            ? DateTimeOffset.UnixEpoch.AddTicks(value / 100)
            : DateTimeOffset.UtcNow;
    }
}
