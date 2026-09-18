using System.Globalization;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace Xmip.Surface;

/// <summary>
/// What a System Process Xmip owns says of itself: its name, its location and
/// its purpose, test or runtime (ADR-0053 clause 3). The name finds a process
/// — every one is <c>xmip-&lt;what&gt;</c> — and the declaration says what it
/// is for. A process writes it where it starts, to one file named for it and
/// its pid, and takes it away where it ends; one that is killed leaves its
/// file behind, and whoever lists the declarations drops those whose process
/// is gone. The same file, in the same place, that <c>xmip-core-node</c>
/// writes for a Rust process.
/// </summary>
public sealed class ProcessDeclaration : IDisposable
{
    /// <summary>The environment variable naming the directory declarations
    /// are written to. Unset, it is <c>xmip/process</c> under the system's
    /// temporary directory, the same for every process on the machine.</summary>
    public const string DirectoryVariable = "XMIP_PROCESS_DIRECTORY";

    /// <summary>The configuration key that says test; anything else, or
    /// nothing, is runtime.</summary>
    public const string PurposeKey = "Xmip:Purpose";

    /// <summary>The word for a process a test started.</summary>
    public const string Test = "test";

    /// <summary>The word for the product doing its work.</summary>
    public const string Runtime = "runtime";

    private ProcessDeclaration(string file)
    {
        File = file;
    }

    /// <summary>The file the declaration was written to.</summary>
    public string File { get; }

    /// <summary>Where declarations are written.</summary>
    public static string Directory()
    {
        string? named = Environment.GetEnvironmentVariable(DirectoryVariable);

        return string.IsNullOrWhiteSpace(named)
            ? Path.Combine(Path.GetTempPath(), "xmip", "process")
            : named;
    }

    /// <summary>The purpose a configuration states: test where it says so,
    /// runtime otherwise, because a process is runtime unless what started it
    /// says test.</summary>
    public static string PurposeOf(IConfiguration configuration)
    {
        string? word = configuration[PurposeKey];

        return string.Equals(word?.Trim(), Test, StringComparison.OrdinalIgnoreCase)
            ? Test
            : Runtime;
    }

    /// <summary>Declare this process where the node says, and take the
    /// declaration away when the process exits. A process that cannot
    /// declare itself still runs: null, and nothing thrown.</summary>
    public static ProcessDeclaration? Declare(string name, string location, string purpose)
    {
        try
        {
            ProcessDeclaration declared = DeclareIn(Directory(), name, location, purpose);
            AppDomain.CurrentDomain.ProcessExit += (_, _) => declared.Dispose();

            return declared;
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Declare this process in a given directory.</summary>
    public static ProcessDeclaration DeclareIn(
        string directory, string name, string location, string purpose)
    {
        System.IO.Directory.CreateDirectory(directory);

        int pid = Environment.ProcessId;
        string file = Path.Combine(directory, $"{name}-{pid}.toml");
        long started = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        string text = string.Create(
            CultureInfo.InvariantCulture,
            $"name = {Quoted(name)}\nlocation = {Quoted(location)}\n" +
            $"purpose = {Quoted(purpose)}\npid = {pid}\nstarted_unix = {started}\n" +
            $"path = {Quoted(Environment.ProcessPath ?? string.Empty)}\n");

        System.IO.File.WriteAllText(
            file, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        return new ProcessDeclaration(file);
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

    // A TOML basic string: a Windows path carries backslashes, which it escapes.
    private static string Quoted(string text)
    {
        return "\"" + text
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal) + "\"";
    }
}
