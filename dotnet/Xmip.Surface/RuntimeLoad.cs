using Xmip.Abi.Operate;

namespace Xmip.Surface;

/// <summary>
/// The runtime library a <see cref="ConfigurationRuntime"/> asks, loaded when
/// first needed and kept. While there is no file at the path — the runtime
/// not built yet — every read tries again; a file that is there and will not
/// load is tried once, and why stays in <see cref="Reason"/>.
/// </summary>
internal sealed class RuntimeLoad(string path) : IDisposable
{
    private readonly Lock _gate = new();
    private Operator? _runtime;
    private string _reason = string.Empty;
    private bool _retry = true;

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

        _runtime = Operator.Load(path, out _reason);

        // Keep trying on every read only while the file is not there yet. A
        // file that is there and refused is a defect to read about.
        _retry = _runtime is null && !File.Exists(path);

        return _runtime;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        using Lock.Scope held = _gate.EnterScope();

        _retry = false;
        _runtime?.Dispose();
        _runtime = null;
    }
}
