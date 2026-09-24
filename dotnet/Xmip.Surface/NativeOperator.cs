using System.Runtime.CompilerServices;
using Xmip.Abi.Module;
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
    private ScopeIndex? _index;

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
    /// runtime said, as the record and the sentence. The table's next read
    /// shows the result either way.
    /// </summary>
    public ConfigurationVerdict Start(string configurationPath)
    {
        return Runtime() is { } runtime
            ? ConfigurationVerdict.Started(configurationPath, runtime.Start(configurationPath))
            : ConfigurationVerdict.NotLoaded(configurationPath, _reason);
    }

    /// <summary>
    /// Validate a node's configuration file without starting it. The file's
    /// text crosses, not its path — the runtime checks a proposed document and
    /// publishes nothing (ADR-0027 clause 9), so the answer carries the
    /// problems itself, and the verdict carries them to whoever asked.
    /// </summary>
    public ConfigurationVerdict Validate(string configurationPath)
    {
        return Runtime() is null
            ? ConfigurationVerdict.NotLoaded(configurationPath, _reason)
            : !File.Exists(configurationPath)
                ? ConfigurationVerdict.NoFile(configurationPath)
                : Validate(configurationPath, File.ReadAllText(configurationPath));
    }

    /// <summary>
    /// Validate the text of a configuration document that is not saved, or
    /// not saved yet — what an editor is holding (ADR-0027, amendment
    /// 2026-09-05). <paramref name="configurationPath"/> names the document
    /// the text is for, in the verdict; nothing is read from it.
    /// </summary>
    public ConfigurationVerdict Validate(string configurationPath, string configuration)
    {
        return Runtime() is { } runtime
            ? ConfigurationVerdict.Validated(configurationPath, runtime.Validate(configuration))
            : ConfigurationVerdict.NotLoaded(configurationPath, _reason);
    }

    /// <inheritdoc />
    public IReadOnlyList<HealthRecord> Health(string scope)
    {
        return Index().Health(scope);
    }

    /// <summary>
    /// The publication as its tree, read across the boundary once per
    /// revision and kept until the runtime's clock moves (ADR-0052, amendment
    /// 2026-09-15): a board that asked the runtime for every row, six figures
    /// each, now asks it once per publication.
    /// </summary>
    public ScopeIndex Index()
    {
        Operator? runtime = Runtime();

        if (runtime is null)
        {
            return ScopeIndex.Empty(Source);
        }

        ulong revision = runtime.SupportsChangeNotifications ? runtime.CurrentRevision : 0;

        lock (_gate)
        {
            if (_index is { } held && revision != 0 && held.Revision == revision)
            {
                return held;
            }
        }

        ScopeIndex built = ScopeIndex.Build(
            runtime.Health(ScopeTree.Root), [], revision, runtime.Source);

        lock (_gate)
        {
            _index = built;
        }

        return built;
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
    public ScopeOperation Control(string scope, ScopeAction action, string who)
    {
        if (Runtime() is not { } runtime)
        {
            return new ScopeOperation(scope, action, false, NotLoaded());
        }

        XmipStatus status = action == ScopeAction.Pause
            ? runtime.PauseScope(scope, who)
            : runtime.ResumeScope(scope);
        string said = action == ScopeAction.Pause
            ? English.Paused(scope, status)
            : English.Resumed(scope, status);

        return new ScopeOperation(scope, action, status == XmipStatus.Ok, said);
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
