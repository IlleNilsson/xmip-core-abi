using System.Runtime.CompilerServices;
using System.Threading.Channels;
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
/// The file is a publication, and its shape — its keys, its words and what
/// a word nobody knows reads as — is <c>observe::Publication</c>'s. This
/// hands the text to the runtime's one reader
/// (<see cref="PublicationReader"/>, <c>xmip_operate.h</c> section 8) and
/// walks nothing itself; until 2026-09-24 it parsed the TOML again, and the
/// Playground's writer and this reader were two writings of one format (open
/// problem 25). A read-only surface: it reports what was published and never
/// acts on it, so <see cref="PauseScope"/> and <see cref="ResumeScope"/>
/// decline. ADR-0027, ADR-0028.
/// </remarks>
public sealed class SnapshotOperator(string path) : IOperatorSurface
{
    private readonly Lock gate = new();

    private Reading? cached;

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
    public RunHeader Run()
    {
        return Read().Run;
    }

    /// <summary>The scope the publisher publishes at — the document's
    /// <c>node</c>, <c>xmip:///A1</c> for a Playground roll named A1 — or the
    /// root when it names none.</summary>
    public string Root()
    {
        return Read().Root;
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

    private sealed record Reading(
        ScopeIndex Index, TopologySnapshot Topology, RunHeader Run, string Root)
    {
        public static Reading Nothing(string source)
        {
            return new Reading(
                ScopeIndex.Empty(source),
                TopologySnapshot.Empty(source),
                RunHeader.None,
                ScopeTree.Root);
        }
    }

    private Reading Read()
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

            Reading? read = Parse(file);

            // The publisher was replacing the file this instant. What was
            // read last still stands, and the stamps are left alone so the
            // next question reads again.
            if (read is null)
            {
                return cached ?? Reading.Nothing(Source);
            }

            Reading fresh = read;
            cached = fresh;
            cachedWrite = file.Exists ? file.LastWriteTimeUtc : default;
            cachedLength = file.Exists ? file.Length : 0;

            return fresh;
        }
    }

    // Null where the file could not be read at all, which is a moment and
    // not a verdict: on Windows a file being renamed over answers a reader
    // with a sharing violation or with access denied, and the second is not
    // an IOException. Until 2026-09-18 it went uncaught, and one such moment
    // ended the prompt's observer for the rest of the session. Text the
    // runtime's reader refuses is nothing published.
    private Reading? Parse(FileInfo file)
    {
        string text;

        try
        {
            if (!file.Exists)
            {
                return Reading.Nothing(Source);
            }

            text = File.ReadAllText(Path);
        }
        catch (Exception exception)
            when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        if (RuntimeLibrary.Rules.Publications.Read(text, out _) is not { } read)
        {
            return Reading.Nothing(Source);
        }

        string source = read.Source.Length > 0 ? read.Source : Source;
        ScopeIndex index = ScopeIndex.Build(
            read.Records,
            read.Counts.Select(count => new ScopeIndex.Count(
                count.Scope, count.Counted, count.Value, null)),
            ++revision,
            source);

        return new Reading(
            index,
            read.Topology ?? TopologySnapshot.Empty(source),
            RunHeader.From(read.Run),
            read.Node.Length > 0 ? read.Node : ScopeTree.Root);
    }
}
