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
    public MeasurementRecord? Measure(string scope, Counted counted)
    {
        ulong value = Read().Counts
            .Where(count => count.Counted == counted && ScopeTree.Beneath(count.Scope, scope))
            .Aggregate(0UL, (sum, count) => sum + count.Value);

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
        IReadOnlyList<CountRecord> Counts);

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
                return new Snapshot([], []);
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

            return new Snapshot(records, counts);
        }
        catch (Exception exception)
            when (exception is IOException or FormatException or InvalidOperationException)
        {
            return new Snapshot([], []);
        }
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
