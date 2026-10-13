namespace Xmip.Surface;

/// <summary>
/// The runtime library asked about a node's configuration, with no node
/// behind it: validate a document, and plan one through
/// <c>xmip_start_v1</c>. These stay calls across the C ABI because nothing
/// runs to be asked; operating a node — its health, figures, scopes,
/// Journeys, Dead Messages, Subscriptions, acts and follow — goes over gRPC
/// to its operate listener (<see cref="ListenerOperator"/>; ADR-0014,
/// amendment 2026-10-13). Turning a status into words is
/// <see cref="ConfigurationVerdict"/>'s.
/// </summary>
/// <remarks>
/// The library is loaded when first needed and kept
/// (<see cref="RuntimeLoad"/>). While there is no file at the path — the
/// runtime not built yet — every call tries again; a file that is there and
/// will not load is tried once, and the reason stays in
/// <see cref="Reason"/>.
/// </remarks>
public sealed class ConfigurationRuntime : IDisposable
{
    private readonly RuntimeLoad _load;

    /// <summary>The runtime at <paramref name="path"/>, found by
    /// <see cref="RuntimeLibrary"/>'s rule.</summary>
    public ConfigurationRuntime(string path)
    {
        Path = path;
        _load = new RuntimeLoad(path);
        _ = _load.Runtime();
    }

    /// <summary>The library this asks.</summary>
    public string Path { get; }

    /// <summary>Whether the runtime is loaded.</summary>
    public bool IsLoaded => _load.Runtime() is not null;

    /// <summary>Why the runtime is not loaded; empty when it is.</summary>
    public string Reason => _load.Runtime() is null ? _load.Reason : string.Empty;

    /// <summary>
    /// Plan a node from its configuration file through <c>xmip_start_v1</c>:
    /// read, build, validate and publish the plan. Nothing is loaded and
    /// nothing runs — a node runs in the program that links its technologies.
    /// Returns what the runtime said, as the record and the sentence.
    /// </summary>
    public ConfigurationVerdict Plan(string configurationPath)
    {
        return _load.Runtime() is { } runtime
            ? ConfigurationVerdict.Planned(configurationPath, runtime.Start(configurationPath))
            : ConfigurationVerdict.NotLoaded(configurationPath, _load.Reason);
    }

    /// <summary>
    /// Validate a node's configuration file without starting it. The file's
    /// text crosses, not its path — the runtime checks a proposed document and
    /// publishes nothing (ADR-0027 clause 9).
    /// </summary>
    public ConfigurationVerdict Validate(string configurationPath)
    {
        return _load.Runtime() is null
            ? ConfigurationVerdict.NotLoaded(configurationPath, _load.Reason)
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
        return _load.Runtime() is { } runtime
            ? ConfigurationVerdict.Validated(configurationPath, runtime.Validate(configuration))
            : ConfigurationVerdict.NotLoaded(configurationPath, _load.Reason);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _load.Dispose();
    }
}
