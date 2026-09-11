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
