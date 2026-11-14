using Microsoft.Extensions.Configuration;

namespace Xmip.Surface;

/// <summary>The one surface a command, a cmdlet or the prompt reads: what the
/// operator stated for one invocation, over the host's document.</summary>
public static partial class SurfaceChoice
{
    /// <summary>
    /// The one surface a command, a cmdlet or the prompt reads, the line over
    /// the document: a node on another machine <paramref name="line"/> names,
    /// else a node on this one it names, else the snapshot it names, else the
    /// document's choice, the first cluster where it names several; else a
    /// <see cref="LocalOperator"/> that names no node and says so
    /// (<see cref="LocalOperator.Unnamed"/>). The runtime library
    /// <see cref="RuntimeLibrary.Stated"/> finds is what every one of them
    /// calls the runtime through, and chooses none. Not yet asked to answer, so
    /// the precedence is tested without a runtime or a network. Held here for
    /// the executable and the PowerShell module alike (ADR-0052 clause 1);
    /// until 2026-09-24 the executable wrote it and the prompt wrote it again.
    /// A node named on the line that gathers its cluster's view answers for
    /// the whole cluster (<see cref="ListenerOperator.Gathers"/>).
    /// </summary>
    /// <param name="line">What the operator stated for this invocation.</param>
    /// <param name="document">The host's document.</param>
    /// <param name="basePath">The directory the document's paths, and a
    /// snapshot named on the line, are written from.</param>
    /// <param name="besideExecutable">Where the runtime library lies by
    /// default: beside the executable, or beside a module pwsh loaded.</param>
    /// <exception cref="InvalidOperationException">What <see cref="Open"/>
    /// refuses, or the line's own <see cref="SurfaceLine.Refusal"/>.</exception>
    public static IOperatorSurface Stated(
        SurfaceLine line, IConfiguration document, string basePath, string besideExecutable)
    {
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(document);

        if (line.Refusal is { } refusal)
        {
            throw new InvalidOperationException(refusal);
        }

        // Found whatever the surface: every one calls the runtime in it.
        _ = RuntimeLibrary.Stated(line.Runtime, document, basePath, besideExecutable);

        return !string.IsNullOrWhiteSpace(line.Remote)
            ? new RemoteOperator(line.Remote, SecureSurface.From(document, basePath))
            : !string.IsNullOrWhiteSpace(line.Local)
                ? new LocalOperator(line.Local)
                : !string.IsNullOrWhiteSpace(line.Snapshot)
                    ? new SnapshotOperator(TomlDocument.Resolve(line.Snapshot, basePath))
                    : IsChosen(document)
                        ? OpenFirst(document, basePath)
                        : new LocalOperator(null);
    }

    /// <summary>
    /// The surface <see cref="Stated"/> chooses from the document at
    /// <paramref name="documentPath"/>, once it has answered: a node
    /// connected, a snapshot file present. Null with the
    /// reason otherwise — the line's own <see cref="SurfaceLine.Refusal"/>,
    /// the document's refusal prefixed with its file name, or the surface's
    /// own <see cref="IOperatorSurface.Source"/> — and the surface released.
    /// The caller disposes what it gets.
    /// </summary>
    public static IOperatorSurface? Answering(
        SurfaceLine line, string documentPath, string besideExecutable, out string reason)
    {
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(documentPath);

        // The line's own refusal is the line's, not the document's.
        if (line.Refusal is { } refusal)
        {
            reason = refusal;
            return null;
        }

        string basePath = Path.GetDirectoryName(Path.GetFullPath(documentPath))
            ?? besideExecutable;
        IOperatorSurface surface;

        try
        {
            surface = Stated(line, TomlDocument.Read(documentPath), basePath, besideExecutable);
        }
        catch (InvalidOperationException misconfigured)
        {
            reason = $"{Path.GetFileName(documentPath)}: {misconfigured.Message}";
            return null;
        }

        if (Answers(surface))
        {
            reason = string.Empty;
            return surface;
        }

        reason = surface.Source;
        (surface as IDisposable)?.Dispose();
        return null;
    }

    /// <summary>Whether a surface answers at all: a node, on this machine or
    /// another, answers at its operate listener; a snapshot file is there. A
    /// surface of another kind is taken at its word.</summary>
    public static bool Answers(IOperatorSurface surface)
    {
        return surface switch
        {
            ListenerOperator node => node.Connect(),
            SnapshotOperator snapshot => snapshot.Exists,
            _ => true,
        };
    }
}
