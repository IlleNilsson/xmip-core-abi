using Xmip.Abi.Operate;

namespace Xmip.Surface;

/// <summary>
/// The runtime library a <see cref="NativeOperator"/> reads, loaded when
/// first needed and kept. While there is no file at the path — the runtime
/// not built yet — every read tries again; a file that is there and will not
/// load is tried once, and why stays in <see cref="Reason"/>, until a watch
/// sees the file made or written again (<see cref="ArrivedAsync"/>).
/// </summary>
internal sealed class RuntimeLoad(string path) : IDisposable
{
    private readonly Lock _gate = new();
    private Operator? _runtime;
    private string _reason = string.Empty;
    private bool _retry = true;
    private bool _disposed;

    // The library as the last load found it; a watch waits for it to differ.
    private PathArrival.Stamp _tried;

    /// <summary>Why the runtime is not loaded, as the last load said.</summary>
    public string Reason
    {
        get
        {
            using Lock.Scope held = _gate.EnterScope();

            return _reason;
        }
    }

    /// <summary>The runtime, loaded now if it may be tried; null where it
    /// is not loaded.</summary>
    public Operator? Runtime()
    {
        using Lock.Scope held = _gate.EnterScope();

        if (_runtime is not null || !_retry)
        {
            return _runtime;
        }

        _tried = PathArrival.Stamp.Of(path);
        _runtime = Operator.Load(path, out _reason);

        // Keep trying on every read only while the file is not there yet. A
        // file that is there and refused is a defect to read about, tried
        // again only once it is written again.
        _retry = _runtime is null && !File.Exists(path);

        return _runtime;
    }

    /// <summary>
    /// The runtime once it loads: at once where it is loaded, otherwise
    /// after each time the file system notices the library made or written
    /// again, tried anew each time — a file refused before among them.
    /// </summary>
    /// <exception cref="OperationCanceledException"><paramref name="stop"/>
    /// was cancelled first.</exception>
    public async Task<Operator> ArrivedAsync(CancellationToken stop)
    {
        while (true)
        {
            if (Runtime() is { } loaded)
            {
                return loaded;
            }

            PathArrival.Stamp tried;
            lock (_gate)
            {
                tried = _tried;
            }

            await PathArrival.ChangedAsync(path, tried, stop).ConfigureAwait(false);

            lock (_gate)
            {
                _retry = _runtime is null && !_disposed;
            }
        }
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
}
