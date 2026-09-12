using System.Runtime.CompilerServices;
using Xmip.Abi.Operate;

namespace Xmip.Surface;

/// <summary>
/// The surface over a loaded runtime: <see cref="IOperatorSurface"/> over
/// <see cref="Operator"/>, the binding in xmip-core-abi. Nothing here crosses
/// the C ABI itself — the binding does that once, for every surface (ADR-0014,
/// amendment of 2026-08-26) — and what is left is turning a status into the
/// words an operator reads, through <see cref="English"/>.
/// </summary>
/// <remarks>
/// The library is loaded when first needed and kept. While there is no file
/// at the path — the runtime not built yet — every read tries again, so the
/// board comes alive the moment it is; a file that is there and will not
/// load is tried once, and the reason stays in <see cref="Source"/>. A
/// surface that cannot reach a node shows that rather than an empty tree.
/// </remarks>
public sealed class NativeOperator : IOperatorSurface, IDisposable
{
    private readonly Lock _gate = new();
    private Operator? _runtime;
    private string _reason = string.Empty;
    private bool _retry = true;
    private bool _disposed;

    /// <summary>Open the runtime at <paramref name="path"/>, found by
    /// <see cref="RuntimeLibrary"/>'s rule.</summary>
    public NativeOperator(string path)
    {
        Path = path;
        _ = Runtime();
    }

    /// <summary>The library this surface loads.</summary>
    public string Path { get; }

    /// <summary>Whether the runtime is loaded and its table in hand.</summary>
    public bool IsLoaded => Runtime() is not null;

    /// <summary>Why the runtime is not loaded; empty when it is.</summary>
    public string Reason => Runtime() is null ? _reason : string.Empty;

    /// <inheritdoc />
    public string Source => Runtime() is { } runtime ? runtime.Source : $"NATIVE — {_reason}";

    /// <summary>
    /// Start a node from its configuration file — as far as the runtime can
    /// today, which is read, build, validate and plan. Returns what the
    /// runtime said. The table's next read shows the result either way.
    /// </summary>
    public string Start(string configurationPath)
    {
        return Runtime() is { } runtime
            ? English.Started(configurationPath, runtime.Start(configurationPath))
            : NotLoaded();
    }

    /// <summary>
    /// Validate a node's configuration file without starting it. The file's
    /// text crosses, not its path — the runtime checks a proposed document and
    /// publishes nothing (ADR-0027 clause 9), so the answer carries the
    /// problems itself.
    /// </summary>
    public string Validate(string configurationPath)
    {
        return Runtime() is not { } runtime
            ? NotLoaded()
            : !File.Exists(configurationPath)
                ? $"no configuration at {configurationPath}"
                : English.Validated(
                    configurationPath, runtime.Validate(File.ReadAllText(configurationPath)));
    }

    /// <inheritdoc />
    public IReadOnlyList<HealthRecord> Health(string scope)
    {
        return Runtime()?.Health(scope) ?? [];
    }

    /// <inheritdoc />
    public TopologySnapshot Topology()
    {
        return TopologySnapshot.Empty(
            $"{Source} — topology is not yet published by the native operator boundary");
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<SurfaceChange> WatchAsync(
        [EnumeratorCancellation] CancellationToken stop = default)
    {
        Operator? runtime = Runtime();
        ulong revision = runtime?.CurrentRevision ?? 0;
        yield return SurfaceChange.Initial(Source);

        if (runtime is null)
        {
            yield break;
        }

        while (!stop.IsCancellationRequested)
        {
            ulong? next;

            if (runtime.SupportsChangeNotifications)
            {
                try
                {
                    next = await Task.Run(
                        () => runtime.WaitForChange(revision, 1_000), stop)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    yield break;
                }
            }
            else
            {
                // A pre-signal runtime remains observable during a rolling
                // upgrade. Current runtimes take the event-driven path above.
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), stop).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    yield break;
                }

                next = revision + 1;
            }

            if (next is not { } changed || changed <= revision)
            {
                continue;
            }

            revision = changed;
            yield return new SurfaceChange(
                revision, SurfaceChangeKind.All, DateTimeOffset.UtcNow, Source);
        }
    }

    /// <inheritdoc />
    public MeasurementRecord? Measure(string scope, Counted counted)
    {
        return Runtime()?.Measure(scope, counted);
    }

    /// <inheritdoc />
    public string PauseScope(string scope, string who)
    {
        return Runtime() is { } runtime
            ? English.Paused(scope, runtime.PauseScope(scope, who))
            : NotLoaded();
    }

    /// <inheritdoc />
    public string ResumeScope(string scope)
    {
        return Runtime() is { } runtime
            ? English.Resumed(scope, runtime.ResumeScope(scope))
            : NotLoaded();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        using Lock.Scope held = _gate.EnterScope();

        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _retry = false;
        _runtime?.Dispose();
        _runtime = null;
    }

    private string NotLoaded()
    {
        return $"no runtime loaded: {_reason}";
    }

    private Operator? Runtime()
    {
        using Lock.Scope held = _gate.EnterScope();

        if (_runtime is not null || !_retry)
        {
            return _runtime;
        }

        _runtime = Operator.Load(Path, out _reason);

        // Keep trying only while the file is not there yet. A file that is
        // there and refused is a defect to read about, not to retry every
        // two seconds.
        _retry = _runtime is null && !File.Exists(Path);

        return _runtime;
    }
}
