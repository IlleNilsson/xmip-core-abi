using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Tomlyn;
using Tomlyn.Model;
using Xmip.Abi.Operate;

namespace Xmip.Surface;

/// <summary>
/// The surface over a published snapshot: what a node — or the Playground,
/// after every tick — wrote to a file, read once per publication into a
/// <see cref="ScopeIndex"/> and answered from that by lookup. The path is
/// given by whoever chose this surface (ADR-0052 clause 3); nothing here
/// guesses at a temp directory, and a path with no file behind it says so in
/// <see cref="Source"/> rather than reporting an empty estate as if it were one.
/// </summary>
/// <remarks>
/// The file is TOML — on disk the estate is TOML, and JSON is reserved for
/// memory and the wire — read as the document it is, not flattened into a
/// configuration: a Playground snapshot is eleven thousand tables, and the
/// flattening cost more than the parse (2026-09-15). A read-only surface: it
/// reports what was published and never acts on it, so
/// <see cref="PauseScope"/> and <see cref="ResumeScope"/> decline. ADR-0027,
/// ADR-0028.
/// </remarks>
public sealed class SnapshotOperator(string path) : IOperatorSurface
{
    private readonly Lock gate = new();

    private Publication? cached;

    private DateTime cachedWrite;

    private long cachedLength;

    private ulong revision;

    /// <summary>The snapshot file this surface reads.</summary>
    public string Path { get; } = path;

    /// <summary>Whether there is a file at <see cref="Path"/> right now.</summary>
    public bool Exists => File.Exists(Path);

    /// <inheritdoc />
    public string Source => Exists ? $"SNAPSHOT — {Path}" : $"SNAPSHOT — no file at {Path}";

    /// <inheritdoc />
    public ScopeIndex Index()
    {
        return Read().Index;
    }

    /// <inheritdoc />
    public IReadOnlyList<HealthRecord> Health(string scope)
    {
        return Read().Index.Health(scope);
    }

    /// <inheritdoc />
    public Figures Figures(string scope)
    {
        return Read().Index.Figures(scope);
    }

    /// <inheritdoc />
    public MeasurementRecord? Measure(string scope, Counted counted)
    {
        return Read().Index.Measure(scope, counted);
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
        ulong announced = 0;

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
                    ++announced, SurfaceChangeKind.All, DateTimeOffset.UtcNow, Source);
            }
        }
        finally
        {
            changed.Writer.TryComplete();
        }
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

    /// <inheritdoc />
    public ScopeOperation Control(string scope, ScopeAction action, string who)
    {
        string said = action == ScopeAction.Pause ? PauseScope(scope, who) : ResumeScope(scope);

        return new ScopeOperation(scope, action, false, said);
    }

    private sealed record Publication(ScopeIndex Index, TopologySnapshot Topology);

    private Publication Read()
    {
        // Parsed once per publication, not once per query: a board asks
        // several times per render, every second, and a Playground snapshot
        // is eleven thousand records. The file's write time and length say
        // whether anything changed.
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

            Publication fresh = Parse(file);
            cached = fresh;
            cachedWrite = file.Exists ? file.LastWriteTimeUtc : default;
            cachedLength = file.Exists ? file.Length : 0;

            return fresh;
        }
    }

    private Publication Parse(FileInfo file)
    {
        try
        {
            if (!file.Exists)
            {
                return new Publication(ScopeIndex.Empty(Source), TopologySnapshot.Empty(Source));
            }

            TomlTable document = TomlSerializer.Deserialize<TomlTable>(
                File.ReadAllText(Path), new TomlSerializerOptions { SourceName = Path })
                ?? [];
            string node = Text(document, "node") ?? ScopeTree.Root;
            string source = Text(document, "source") ?? Source;

            List<HealthRecord> records = [];

            foreach (TomlTable row in Rows(document, "records"))
            {
                records.Add(new HealthRecord(
                    Text(row, "scope") ?? string.Empty,
                    ParseState(Text(row, "state")),
                    (byte)Math.Clamp(Number(row, "severity"), 0, byte.MaxValue),
                    Text(row, "evidence") ?? string.Empty,
                    ParseObserved(row, "observed_unix_nanos")));
            }

            List<ScopeIndex.Count> counts = [];

            foreach (TomlTable row in Rows(document, "counts"))
            {
                counts.Add(new ScopeIndex.Count(
                    node,
                    ParseCounted(Text(row, "counted")),
                    (ulong)Math.Max(0, Number(row, "value")),
                    null));
            }

            ScopeIndex index = ScopeIndex.Build(records, counts, ++revision, source);

            return new Publication(index, ParseTopology(document, source));
        }
        catch (Exception exception)
            when (exception is IOException or FormatException or InvalidOperationException
                or TomlException)
        {
            return new Publication(ScopeIndex.Empty(Source), TopologySnapshot.Empty(Source));
        }
    }

    private static TopologySnapshot ParseTopology(TomlTable document, string source)
    {
        TomlTable? topology = document.TryGetValue("topology", out object? found)
            ? found as TomlTable
            : null;

        if (topology is null)
        {
            return TopologySnapshot.Empty(source);
        }

        List<TopologyNode> nodes = [];

        foreach (TomlTable row in Rows(topology, "nodes"))
        {
            nodes.Add(new TopologyNode(
                Text(row, "id") ?? string.Empty,
                EmptyAsNull(Text(row, "parent")),
                Text(row, "label") ?? Text(row, "id") ?? string.Empty,
                ParseNodeKind(Text(row, "kind")),
                Text(row, "scope") ?? string.Empty,
                ParseState(Text(row, "state")),
                ParseOrigin(Text(row, "origin")),
                Real(row, "load"),
                Real(row, "activity"),
                Text(row, "evidence") ?? string.Empty));
        }

        List<CommunicationLink> links = [];

        foreach (TomlTable row in Rows(topology, "links"))
        {
            links.Add(new CommunicationLink(
                Text(row, "id") ?? string.Empty,
                Text(row, "from") ?? string.Empty,
                Text(row, "to") ?? string.Empty,
                ParsePattern(Text(row, "pattern")),
                ParseOrigin(Text(row, "origin")),
                Text(row, "protocol") ?? string.Empty,
                ParseState(Text(row, "state")),
                (ulong)Math.Max(0, Number(row, "volume")),
                Real(row, "rate"),
                Real(row, "latency_ms"),
                Real(row, "progress"),
                (uint)Math.Clamp(Number(row, "attempts"), 0, uint.MaxValue),
                Text(row, "evidence") ?? string.Empty));
        }

        return new TopologySnapshot(
            nodes,
            links,
            ParseObserved(topology, "observed_unix_nanos"),
            Text(topology, "source") ?? source);
    }

    private static TomlTableArray Rows(TomlTable table, string key)
    {
        return table.TryGetValue(key, out object? found) && found is TomlTableArray rows
            ? rows
            : [];
    }

    private static string? Text(TomlTable table, string key)
    {
        return table.TryGetValue(key, out object? found)
            ? found switch
            {
                string text => text,
                null => null,
                _ => Convert.ToString(found, CultureInfo.InvariantCulture),
            }
            : null;
    }

    private static long Number(TomlTable table, string key)
    {
        return table.TryGetValue(key, out object? found)
            ? found switch
            {
                long value => value,
                double value => (long)value,
                string text when long.TryParse(text, out long value) => value,
                _ => 0,
            }
            : 0;
    }

    private static double Real(TomlTable table, string key)
    {
        return table.TryGetValue(key, out object? found)
            ? found switch
            {
                double value => value,
                long value => value,
                string text when double.TryParse(
                    text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
                    => value,
                _ => 0D,
            }
            : 0D;
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

    // The word is English's, read back through English's own inverse; a word
    // this build does not know is Stressed, so it shows and is looked at.
    private static HealthState ParseState(string? state)
    {
        return English.MoodOf(state) ?? HealthState.Stressed;
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

    private static DateTimeOffset ParseObserved(TomlTable table, string key)
    {
        return table.TryGetValue(key, out object? found) && found is long nanos
            ? DateTimeOffset.UnixEpoch.AddTicks(nanos / 100)
            : DateTimeOffset.UtcNow;
    }
}
