using Xmip.Abi.Module;

namespace Xmip.Abi.Tests;

/// <summary>Section 1 of <c>xmip_module.h</c>: the version, the entrypoint
/// and the file name a host looks for.</summary>
public sealed class ModuleAbiTests
{
    [Fact]
    public void SpeaksTheAbiVersionTheHeaderDeclares()
    {
        Assert.Equal(Header.Define("xmip_module.h", "XMIP_ABI_VERSION"), $"{ModuleAbi.AbiVersion}");
    }

    [Fact]
    public void NamesTheEntrypointTheHeaderDeclares()
    {
        // The header is the expected value in spirit; xUnit2000 wants the
        // constant first, and the assertion means the same either way round.
        Assert.Equal(ModuleAbi.Entrypoint, Header.Define("xmip_module.h", "XMIP_ENTRYPOINT"));
    }

    [Fact]
    public void NamesTheLibraryTheWayThisPlatformDoes()
    {
        // Getting this wrong means the probe looks for a file that is never
        // there, on whichever platform nobody tested.
        string name = ModuleAbi.LibraryFileName("xmip_core_transport_file");

        if (OperatingSystem.IsWindows())
        {
            Assert.Equal("xmip_core_transport_file.dll", name);
        }
        else if (OperatingSystem.IsMacOS())
        {
            Assert.Equal("libxmip_core_transport_file.dylib", name);
        }
        else
        {
            Assert.Equal("libxmip_core_transport_file.so", name);
        }
    }
}
