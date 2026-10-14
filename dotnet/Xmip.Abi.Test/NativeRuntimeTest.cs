using Xmip.Abi.Operate;

namespace Xmip.Abi.Test;

/// <summary>
/// The one load rule for the runtime's library (<see cref="NativeRuntime"/>):
/// a copy on a desktop, by name on Android, the main program on iOS
/// (ADR-0014, amendment 2026-10-14).
/// </summary>
public sealed class NativeRuntimeTest
{
    [Fact]
    public void ADesktopLoadsACopy()
    {
        Assert.Equal(NativeLoading.Copy, NativeRuntime.For(android: false, ios: false));
    }

    [Fact]
    public void AndroidLoadsThePackagedLibraryByName()
    {
        Assert.Equal(NativeLoading.ByName, NativeRuntime.For(android: true, ios: false));
    }

    [Fact]
    public void IosTakesTheMainProgram()
    {
        Assert.Equal(NativeLoading.MainProgram, NativeRuntime.For(android: false, ios: true));
    }

    [Fact]
    public void ThisProcessIsADesktop()
    {
        Assert.Equal(NativeLoading.Copy, NativeRuntime.Loading);
    }

    [Fact]
    public void TheFileNameIsThePlatformsOwn()
    {
        string name = NativeRuntime.FileName;

        Assert.Equal(
            OperatingSystem.IsWindows()
                ? "xmip_core_runtime.dll"
                : OperatingSystem.IsMacOS()
                    ? "libxmip_core_runtime.dylib"
                    : "libxmip_core_runtime.so",
            name);
    }

    [Fact]
    public void ADesktopLoadsTheBuiltLibraryFromACopy()
    {
        string built = Path.Combine(AppContext.BaseDirectory, NativeRuntime.FileName);

        Assert.True(File.Exists(built), $"no runtime at {built}: run cargo build first");
        Assert.True(NativeRuntime.TryLoad(built, out nint library, out string reason), reason);

        try
        {
            Assert.NotEqual(0, library);
            Assert.Empty(reason);
        }
        finally
        {
            NativeRuntime.Free(library);
        }
    }
}
