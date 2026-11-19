using Xmip.Abi.Module;

namespace Xmip.Abi.Test;

/// <summary>Section 1 of <c>xmip_module.h</c>: the version and the
/// entrypoint.</summary>
public sealed class ModuleAbiTest
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
}
