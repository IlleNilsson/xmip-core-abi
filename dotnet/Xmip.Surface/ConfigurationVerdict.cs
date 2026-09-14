using Xmip.Abi.Module;
using Xmip.Abi.Operate;

namespace Xmip.Surface;

/// <summary>
/// What came of handing the runtime a node configuration — to validate, or
/// to start: the document, the status, the problems the runtime listed, and
/// the sentence every surface says about it. A surface decides from the
/// record and shows the sentence; it never learns the verdict by reading its
/// own English back (ADR-0052 clause 4).
/// </summary>
/// <param name="Path">The configuration document that was handed over.</param>
/// <param name="Status">What the runtime answered, or why it could not be
/// asked: <see cref="XmipStatus.Unavailable"/> when no runtime is loaded,
/// <see cref="XmipStatus.NotFound"/> when there is no file at the path.</param>
/// <param name="Problems">What the runtime found wrong, one line each; empty
/// when it found nothing, or when the call itself failed.</param>
/// <param name="Said">The sentence for a person — <see cref="English"/>'s.</param>
public sealed record ConfigurationVerdict(
    string Path, XmipStatus Status, IReadOnlyList<string> Problems, string Said)
{
    /// <summary>The runtime did what was asked and had nothing to report.</summary>
    public bool Ok => Status == XmipStatus.Ok && Problems.Count == 0;

    /// <summary>The verdict when no runtime is loaded to ask.</summary>
    public static ConfigurationVerdict NotLoaded(string path, string reason)
    {
        return new(path, XmipStatus.Unavailable, [], $"no runtime loaded: {reason}");
    }

    /// <summary>The verdict when there is no document at the path.</summary>
    public static ConfigurationVerdict NoFile(string path)
    {
        return new(path, XmipStatus.NotFound, [], $"no configuration at {path}");
    }

    /// <summary>The runtime's answer to a validation, with the sentence.</summary>
    public static ConfigurationVerdict Validated(string path, ValidationRecord answer)
    {
        return new(path, answer.Status, answer.Problems, English.Validated(path, answer));
    }

    /// <summary>The runtime's answer to a start, with the sentence.</summary>
    public static ConfigurationVerdict Started(string path, XmipStatus status)
    {
        return new(path, status, [], English.Started(path, status));
    }
}
