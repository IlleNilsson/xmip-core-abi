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
///
/// A followed surface is told, it does not ask (ADR-0052, amendment
/// 2026-09-15): while anything follows <see cref="WatchAsync"/>, the follow
/// reads each publication once, before announcing it and off whoever renders,
/// and every question is answered from that reading without touching the
/// file. Until 2026-10-03 the page rendered after each Playground tick read
/// the 2 MB snapshot itself (867 ms, measured 2026-10-02).
/// </remarks>
public sealed class SnapshotOperator(string path) : IOperatorSurface
{
    private readonly Lock gate = new();
    private volatile Reading? cached;
    private DateTime cachedWrite;
    private long cachedLength;
    private ulong revision;

    // How many follows are reading this publication as it changes.
    private int following;

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

    /// <inheritdoc cref="IOperatorSurface.Root" />
    /// <remarks>The document's <c>node</c>, <c>xmip:///&lt;cluster&gt;</c> for a
    /// Playground roll of that cluster, where a record is published beneath it; the root when
    /// it names none, or names a scope nothing is published at — a drill
    /// cannot start where there is nothing to drill.</remarks>
    public string Root()
    {
        Reading read = Read();

        return read.Index.Health(read.Root).Count > 0 ? read.Root : ScopeTree.Root;
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

        // Any notice is a reason to look again; a watcher whose buffer
        // overflowed has lost notices, which is one too.
        void Signal(object sender, EventArgs arguments)
        {
            changed.Writer.TryWrite(true);
        }

        watcher.Changed += Signal;
        watcher.Created += Signal;
        watcher.Deleted += Signal;
        watcher.Renamed += Signal;
        watcher.Error += Signal;
        watcher.EnableRaisingEvents = true;

        // Attach before reading the current view. A replacement racing that
        // read is then either already read or queued as a change.
        Refresh(out bool current);
        if (!current)
        {
            changed.Writer.TryWrite(true);
        }

        Interlocked.Increment(ref following);
        ulong announced = 0;

        try
        {
            yield return SurfaceChange.Initial(Source);

            await foreach (bool notice in changed.Reader.ReadAllAsync(stop).ConfigureAwait(false))
            {
                _ = notice;

                // Publishers replace atomically. A short yield also coalesces
                // the several filesystem notifications one replacement can emit.
                await Task.Delay(TimeSpan.FromMilliseconds(25), stop).ConfigureAwait(false);
                while (changed.Reader.TryRead(out _))
                {
                }

                // Read here, on the follow, so whoever the announcement wakes
                // finds the publication read. A read that met the publisher
                // mid-replace is tried again rather than left stale.
                Refresh(out current);

                if (!current)
                {
                    changed.Writer.TryWrite(true);
                    continue;
                }

                yield return new SurfaceChange(
                    ++announced, SurfaceChangeKind.All, DateTimeOffset.UtcNow, Source);
            }
        }
        finally
        {
            Interlocked.Decrement(ref following);
            changed.Writer.TryComplete();
        }
    }

    /// <inheritdoc />
    public EventSubscriptionList EventSubscriptions()
    {
        return Read().EventSubscriptions;
    }

    /// <inheritdoc cref="IOperatorSurface.Act(EventSubscriptionRecord, EventSubscriptionAct, string)" />
    /// <remarks>A snapshot touches no node. Where its publication says where
    /// its publisher takes orders, the act is left there for the node that
    /// holds the Event subscription, which applies it at its next look and
    /// publishes what came of it (<c>observe::Order</c>, ADR-0065, amendment
    /// 2026-09-29); where it says nowhere, the act is declined.</remarks>
    public EventSubscriptionOperation Act(
        EventSubscriptionRecord subscription, EventSubscriptionAct act, string who)
    {
        ArgumentNullException.ThrowIfNull(subscription);

        (bool left, string said) = SnapshotOrder.Leave(
            Read().EventSubscriptions.Orders, subscription.Node, "event-subscription",
            subscription.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
            EventSubscriptionOperation.Word(act), who, $"Event subscription {subscription.Id}");

        return new EventSubscriptionOperation(subscription.Node, subscription.Id, act, left, said);
    }

    /// <inheritdoc />
    public SubscriptionList Subscriptions()
    {
        return Read().Subscriptions;
    }

    /// <inheritdoc cref="IOperatorSurface.Act(SubscriptionRecord, SubscriptionAct, string)" />
    /// <remarks>A snapshot touches no node. Where its publication says where
    /// its publisher takes orders, the act is left there for the node that
    /// routes by the Subscription, which applies it at its next look and
    /// publishes what came of it (<c>observe::Order</c>, ADR-0013, amendment
    /// 2026-09-30); where it says nowhere, the act is declined.</remarks>
    public SubscriptionOperation Act(
        SubscriptionRecord subscription, SubscriptionAct act, string who)
    {
        ArgumentNullException.ThrowIfNull(subscription);

        (bool left, string said) = SnapshotOrder.Leave(
            Read().Subscriptions.Orders, subscription.Node, "subscription", subscription.Name,
            SubscriptionOperation.Word(act), who, $"Subscription '{subscription.Name}'");

        return new SubscriptionOperation(subscription.Node, subscription.Name, act, left, said);
    }

    /// <inheritdoc />
    public DeadMessageList DeadMessages()
    {
        return Read().DeadMessages;
    }

    /// <inheritdoc cref="IOperatorSurface.Act(DeadMessageRecord, DeadMessageAct, string)" />
    /// <remarks>A snapshot touches no node. Where its publication says where
    /// its publisher takes orders, the Replay is left there for the node whose
    /// queue keeps the Message, which takes it at its next look and publishes
    /// what came of it (<c>observe::Order</c>, ADR-0052, amendment
    /// 2026-10-01); where it says nowhere, the act is declined.</remarks>
    public DeadMessageOperation Act(DeadMessageRecord message, DeadMessageAct act, string who)
    {
        ArgumentNullException.ThrowIfNull(message);

        (bool left, string said) = SnapshotOrder.Leave(
            Read().DeadMessages.Orders, message.Node, DeadMessageOperation.Noun, message.Message,
            DeadMessageOperation.Word(act), who, $"Message {message.Message}");

        return new DeadMessageOperation(message.Node, message.Message, act, left, said);
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
        ScopeIndex Index,
        TopologySnapshot Topology,
        RunHeader Run,
        string Root,
        SubscriptionList Subscriptions,
        EventSubscriptionList EventSubscriptions,
        DeadMessageList DeadMessages)
    {
        public static Reading Nothing(string source)
        {
            return new Reading(
                ScopeIndex.Empty(source),
                TopologySnapshot.Empty(source),
                RunHeader.None,
                ScopeTree.Root,
                SubscriptionList.Empty,
                EventSubscriptionList.Empty,
                DeadMessageList.Empty);
        }
    }

    // A follow's last reading while one follows; the file as it is otherwise.
    private Reading Read()
    {
        return Volatile.Read(ref following) > 0 && cached is { } told ? told : Refresh(out _);
    }

    // Parsed once per publication, never per question: the file's write time
    // and length say whether anything changed.
    private Reading Refresh(out bool current)
    {
        lock (gate)
        {
            FileInfo file = new(Path);
            current = true;

            if (cached is { } held
                && file.Exists
                && file.LastWriteTimeUtc == cachedWrite
                && file.Length == cachedLength)
            {
                return held;
            }

            Reading? read = Parse(file);

            // The publisher was replacing the file this instant: what was read
            // last stands, and the stamps stay so the next question reads again.
            if (read is null)
            {
                current = false;
                return cached ?? Reading.Nothing(Source);
            }

            cachedWrite = file.Exists ? file.LastWriteTimeUtc : default;
            cachedLength = file.Exists ? file.Length : 0;
            cached = read;

            return read;
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
        // Each count keeps when it was observed, so a figure is as old as its
        // publication says: until 2026-09-26 it was dropped here, and every
        // figure a snapshot answered was dated now, however long the
        // publisher had been silent (ADR-0027 clause 6).
        ScopeIndex index = ScopeIndex.Build(
            read.Records,
            read.Counts.Select(count => new ScopeIndex.Count(
                count.Scope, count.Counted, count.Value, count.Observed)),
            ++revision,
            source);

        return new Reading(
            index,
            read.Topology ?? TopologySnapshot.Empty(source),
            RunHeader.From(read.Run),
            read.Node.Length > 0 ? read.Node : ScopeTree.Root,
            read.Subscriptions,
            read.EventSubscriptions,
            read.DeadMessages);
    }
}
