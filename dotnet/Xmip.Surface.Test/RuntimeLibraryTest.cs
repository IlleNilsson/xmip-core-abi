namespace Xmip.Surface.Test;

/// <summary>
/// One rule for finding the runtime, in order: configuration, environment,
/// beside the executable (ADR-0052 clause 1).
/// </summary>
public sealed class RuntimeLibraryTest
{
    private static readonly string Base = Path.Combine(Path.GetTempPath(), "xmip-surface-test");
    private static readonly string Beside = Path.Combine(Path.GetTempPath(), "xmip-surface-exe");

    [Fact]
    public void ConfigurationWinsAndResolvesAgainstTheBasePath()
    {
        string chosen = RuntimeLibrary.Choose(
            "lib/runtime.dll", "/elsewhere/runtime.dll", Base, Beside);

        Assert.Equal(Path.GetFullPath(Path.Combine(Base, "lib", "runtime.dll")), chosen);
    }

    [Fact]
    public void AnAbsoluteConfiguredPathIsTakenAsIs()
    {
        string absolute = Path.Combine(Base, "runtime.dll");

        Assert.Equal(absolute, RuntimeLibrary.Choose(absolute, null, Beside, Beside));
    }

    [Fact]
    public void TheEnvironmentIsNextWhenConfigurationIsBlank()
    {
        string fromEnvironment = Path.Combine(Base, "env-runtime.dll");

        Assert.Equal(fromEnvironment, RuntimeLibrary.Choose("  ", fromEnvironment, Base, Beside));
        Assert.Equal(fromEnvironment, RuntimeLibrary.Choose(null, fromEnvironment, Base, Beside));
    }

    [Fact]
    public void BesideTheExecutableIsLast()
    {
        string chosen = RuntimeLibrary.Choose(null, "", Base, Beside);

        Assert.Equal(Path.Combine(Beside, RuntimeLibrary.FileName), chosen);
    }

    [Fact]
    public void TheFileNameIsThePlatformsOwn()
    {
        string name = RuntimeLibrary.FileName;

        Assert.Contains("xmip_core_runtime", name, StringComparison.Ordinal);
        Assert.True(name.EndsWith(".dll", StringComparison.Ordinal)
            || name.EndsWith(".so", StringComparison.Ordinal)
            || name.EndsWith(".dylib", StringComparison.Ordinal));
    }
}
