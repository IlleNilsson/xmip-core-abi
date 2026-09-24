using Microsoft.Extensions.Configuration;

namespace Xmip.Surface;

/// <summary>
/// Where the runtime's native library is, by one rule for every surface
/// (ADR-0052 clause 1): the configuration's <c>Xmip:RuntimeLibrary</c>, else
/// the <c>XMIP_RUNTIME_LIBRARY</c> environment variable, else the library
/// beside the executable. The language server keeps the same rule in Rust.
/// </summary>
public static class RuntimeLibrary
{
    /// <summary>The configuration key, in the host's <c>[Xmip]</c> table.</summary>
    public const string ConfigurationKey = "Xmip:RuntimeLibrary";

    /// <summary>The environment variable an operator sets once per machine.</summary>
    public const string EnvironmentVariable = "XMIP_RUNTIME_LIBRARY";

    /// <summary>The library's file name on this platform.</summary>
    public static string FileName =>
        OperatingSystem.IsWindows()
            ? "xmip_core_runtime.dll"
            : OperatingSystem.IsMacOS()
                ? "libxmip_core_runtime.dylib"
                : "libxmip_core_runtime.so";

    /// <summary>The library this process should load. A relative path in the
    /// configuration resolves against <paramref name="basePath"/>, the
    /// directory the configuration's paths are written from.</summary>
    public static string Find(IConfiguration configuration, string basePath)
    {
        return Choose(
            configuration[ConfigurationKey],
            Environment.GetEnvironmentVariable(EnvironmentVariable),
            basePath,
            AppContext.BaseDirectory);
    }

    /// <summary>
    /// The library an operator stated, over the rule: a path typed for this
    /// one invocation — <c>xmip-cli --runtime</c>, <c>-Library</c> on a cmdlet
    /// — wins and is taken from the current directory; a blank one is no
    /// statement, and then <paramref name="configuration"/>, the environment
    /// and <paramref name="besideExecutable"/> decide as <see cref="Choose"/>
    /// says. One precedence for the command line and PowerShell (ADR-0052
    /// clause 1); until 2026-09-24 the cli held it alone and every cmdlet
    /// demanded a path.
    /// </summary>
    public static string Stated(
        string? overridden, IConfiguration configuration, string basePath, string besideExecutable)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return string.IsNullOrWhiteSpace(overridden)
            ? Choose(
                configuration[ConfigurationKey],
                Environment.GetEnvironmentVariable(EnvironmentVariable),
                basePath,
                besideExecutable)
            : Path.GetFullPath(overridden);
    }

    /// <summary>The rule itself, with every input in hand: configuration
    /// first, then the environment, then beside the executable.</summary>
    public static string Choose(
        string? configured, string? fromEnvironment, string basePath, string besideExecutable)
    {
        return !string.IsNullOrWhiteSpace(configured)
            ? TomlDocument.Resolve(configured, basePath)
            : !string.IsNullOrWhiteSpace(fromEnvironment)
                ? Path.GetFullPath(fromEnvironment)
                : Path.Combine(besideExecutable, FileName);
    }
}
