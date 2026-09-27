using Microsoft.Extensions.Configuration;
using Xmip.Abi.Module;
using Xmip.Abi.Operate;

namespace Xmip.Surface;

/// <summary>
/// What a System Process Xmip owns says of itself: its name, its location and
/// its purpose, test or runtime (ADR-0053 clause 3). The name finds a process
/// — every one is <c>xmip-&lt;what&gt;</c> — and the declaration says what it
/// is for. A process declares itself where it starts and takes the
/// declaration away where it ends; one that is killed leaves its file behind,
/// and whoever lists the declarations drops those whose process is gone.
/// </summary>
/// <remarks>
/// The file, its directory, its words and its reading are
/// <c>xmip-core-node</c>'s, reached through the runtime's library
/// (<c>xmip_operate.h</c> section 13, <see cref="RuntimeProcesses"/>): this
/// writes and reads no file of its own. Until 2026-09-27 it wrote the file
/// again, took any purpose but <c>test</c> for runtime, and the estate's
/// PowerShell module read the files with a TOML reader of its own.
/// </remarks>
public sealed class ProcessDeclaration : IDisposable
{
    /// <summary>The configuration key that states the purpose.</summary>
    public const string PurposeKey = "Xmip:Purpose";

    /// <summary>The word for the product doing its work, which a process is
    /// unless what started it states otherwise.</summary>
    public const string Runtime = "runtime";

    private ProcessDeclaration(string file)
    {
        File = file;
    }

    /// <summary>The file the declaration stands in.</summary>
    public string File { get; }

    /// <summary>The purpose a configuration states, as it states it; runtime
    /// where it states none. Whether the word is a purpose is the node's to
    /// judge, and <see cref="Declare"/> is refused when it is not.</summary>
    public static string PurposeOf(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        string? word = configuration[PurposeKey];

        return string.IsNullOrWhiteSpace(word) ? Runtime : word;
    }

    /// <summary>
    /// Declare this process where the node says, and take the declaration
    /// away when the process exits. A process whose declaration could not be
    /// written, or that has no runtime library to declare through, still
    /// runs: null, and nothing thrown. <paramref name="library"/> is the
    /// runtime library the program was told to load, as
    /// <see cref="ProgramAudit"/> takes it.
    /// </summary>
    /// <exception cref="ArgumentException">The node refused what was declared
    /// — a purpose that is no purpose word — in its own sentence.</exception>
    public static ProcessDeclaration? Declare(
        string name, string location, string purpose, string? library = null)
    {
        if (!string.IsNullOrWhiteSpace(library))
        {
            RuntimeLibrary.Prefer(library);
        }

        RuntimeProcesses processes;

        try
        {
            processes = RuntimeLibrary.Rules.Processes;
        }
        catch (InvalidOperationException)
        {
            return null;
        }

        XmipStatus status = processes.Declare(name, location, purpose, null, out string answer);

        if (status == XmipStatus.Invalid)
        {
            throw new ArgumentException(answer, nameof(purpose));
        }

        if (status != XmipStatus.Ok)
        {
            return null;
        }

        ProcessDeclaration declared = new(answer);
        AppDomain.CurrentDomain.ProcessExit += (_, _) => declared.Dispose();

        return declared;
    }

    /// <summary>Every declaration standing in <paramref name="directory"/>,
    /// null or empty for the directory the node names, whether or not its
    /// process still runs — which the caller, who can see the operating
    /// system's processes, judges.</summary>
    /// <exception cref="InvalidOperationException">No runtime library could be
    /// loaded to read them through.</exception>
    public static ProcessDeclarations Standing(string? directory = null)
    {
        return RuntimeLibrary.Rules.Processes.Read(directory);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        try
        {
            System.IO.File.Delete(File);
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            // Gone already, or held by a reader: whoever lists drops it.
        }
    }
}
