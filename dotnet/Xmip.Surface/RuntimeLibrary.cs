using Microsoft.Extensions.Configuration;
using Xmip.Abi.Operate;

namespace Xmip.Surface;

/// <summary>
/// Where the runtime's native library is, by one rule for every surface
/// (ADR-0052 clause 1): the configuration's <c>Xmip:RuntimeLibrary</c>, else
/// the <c>XMIP_RUNTIME_LIBRARY</c> environment variable, else the library
/// beside the executable — and the one library this process calls the
/// runtime's rules in (<see cref="Rules"/>).
/// </summary>
/// <remarks>
/// The rule is written here and nowhere else in the estate. A surface has to
/// find the runtime before it can call anything in it, so this is the one
/// rule a .NET surface cannot ask the runtime for; the Rust copy the language
/// server kept went on 2026-09-24, and that server is told its library
/// (ADR-0052, amendment 2026-09-24).
/// </remarks>
public static class RuntimeLibrary
{
    /// <summary>The configuration key, in the host's <c>[Xmip]</c> table.</summary>
    public const string ConfigurationKey = "Xmip:RuntimeLibrary";

    /// <summary>The environment variable an operator sets once per machine.</summary>
    public const string EnvironmentVariable = "XMIP_RUNTIME_LIBRARY";

    private static readonly Lock Gate = new();

    private static volatile RuntimeRules? loaded;

    private static string? preferred;

    /// <summary>The library's file name on this platform.</summary>
    public static string FileName =>
        OperatingSystem.IsWindows()
            ? "xmip_core_runtime.dll"
            : OperatingSystem.IsMacOS()
                ? "libxmip_core_runtime.dylib"
                : "libxmip_core_runtime.so";

    /// <summary>
    /// Where this build of the surface lies — beside the executable for the
    /// command line and the GUI hosts, beside the module for PowerShell, whose
    /// executable is pwsh — and so where the runtime library lies by default.
    /// </summary>
    public static string Beside =>
        Path.GetDirectoryName(typeof(RuntimeLibrary).Assembly.Location) is { Length: > 0 } here
            ? here
            : AppContext.BaseDirectory;

    /// <summary>
    /// The runtime's rules — scope containment, the stage words and their
    /// parse, a mood's word and color name, the worst-first order — in the
    /// library this process loads, once. Every surface calls these and keeps
    /// no rule of its own (<c>xmip_operate.h</c> section 7). The library is
    /// the one a surface's own configuration found (<see cref="Find"/>,
    /// <see cref="Stated"/>), else the rule with nothing configured.
    /// </summary>
    /// <exception cref="InvalidOperationException">No runtime library could be
    /// loaded; the message says where it looked and how to put one there.</exception>
    public static RuntimeRules Rules
    {
        get
        {
            // Asked thousands of times a render; loaded once, then read freely.
            if (loaded is { } ready)
            {
                return ready;
            }

            lock (Gate)
            {
                if (loaded is not null)
                {
                    return loaded;
                }

                string fallback = Choose(
                    null, Environment.GetEnvironmentVariable(EnvironmentVariable), Beside, Beside);
                string reason = string.Empty;

                foreach (string path in new[] { preferred, fallback }.OfType<string>().Distinct())
                {
                    loaded = RuntimeRules.Load(path, out reason);

                    if (loaded is not null)
                    {
                        return loaded;
                    }
                }

                throw new InvalidOperationException(
                    $"The runtime's rules are not loaded: {reason}. Build it (cargo build in " +
                    $"module/platform/runtime), set {EnvironmentVariable}, or name " +
                    $"{ConfigurationKey} in the surface's document.");
            }
        }
    }

    /// <summary>The library this process should load. A relative path in the
    /// configuration resolves against <paramref name="basePath"/>, the
    /// directory the configuration's paths are written from.</summary>
    public static string Find(IConfiguration configuration, string basePath)
    {
        return Prefer(Choose(
            configuration[ConfigurationKey],
            Environment.GetEnvironmentVariable(EnvironmentVariable),
            basePath,
            AppContext.BaseDirectory));
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

        return Prefer(string.IsNullOrWhiteSpace(overridden)
            ? Choose(
                configuration[ConfigurationKey],
                Environment.GetEnvironmentVariable(EnvironmentVariable),
                basePath,
                besideExecutable)
            : Path.GetFullPath(overridden));
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

    // The library a surface's own configuration found is the one its rules
    // are called in, until they are: the first found wins, and a path that
    // does not load falls back to the rule with nothing configured.
    private static string Prefer(string path)
    {
        lock (Gate)
        {
            if (loaded is null)
            {
                preferred ??= path;
            }
        }

        return path;
    }
}
