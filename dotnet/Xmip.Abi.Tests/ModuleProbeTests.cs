using Xmip.Abi.Module;
using Xmip.Abi.Operate;

namespace Xmip.Abi.Tests;

/// <summary>
/// What a probe judges of a module that answered, once for <c>xmip-cli
/// probe</c> and <c>Get-XmipModuleDescriptor</c>, and the two boundaries both
/// report. Probing a real library is the conformance fixture's; these hold
/// the judgement over what a module said.
/// </summary>
public sealed class ModuleProbeTests
{
    private static ModuleProbe.Result Said(
        XmipStatus status = XmipStatus.Ok,
        string provider = "core",
        string standard = "",
        uint abiVersion = ModuleAbi.AbiVersion)
    {
        return new ModuleProbe.Result(
            status, provider, "file", standard, abiVersion, "1.0", "0.1.0", string.Empty);
    }

    [Fact]
    public void AModuleThatSaysWhatTheHeaderAsksConforms()
    {
        ModuleProbe.Result result = Said();

        Assert.True(result.Conforms);
        Assert.Equal(string.Empty, result.Complaint);
    }

    [Fact]
    public void AnotherAbiVersionIsSaidWithBothNumbers()
    {
        ModuleProbe.Result result = Said(abiVersion: ModuleAbi.AbiVersion + 1);

        Assert.False(result.Conforms);
        Assert.StartsWith("Loaded, and disagrees", result.Complaint, StringComparison.Ordinal);
        Assert.Contains($"{ModuleAbi.AbiVersion + 1}", result.Complaint, StringComparison.Ordinal);
    }

    [Fact]
    public void ACoreModuleNamingAStandardDoesNotConform()
    {
        ModuleProbe.Result core = Said(standard: "rfc-9110");
        ModuleProbe.Result provider = Said(provider: "example", standard: "rfc-9110");

        Assert.False(core.Conforms);
        Assert.Contains("ADR-0011", core.Complaint, StringComparison.Ordinal);
        Assert.True(provider.Conforms);
    }

    [Fact]
    public void AModuleThatRefusedHasNoComplaintAndDoesNotConform()
    {
        // The status says why; a complaint is about a module that loaded.
        ModuleProbe.Result refused = Said(status: XmipStatus.Unsupported);

        Assert.False(refused.Conforms);
        Assert.Equal(string.Empty, refused.Complaint);
    }

    [Fact]
    public void BothBoundariesAreTheBindingsConstants()
    {
        AbiBoundaries current = AbiBoundaries.Current;

        Assert.Equal(ModuleAbi.AbiVersion, current.ModuleVersion);
        Assert.Equal(ModuleAbi.Entrypoint, current.ModuleEntrypoint);
        Assert.Equal(
            ModuleAbi.LibraryFileName(AbiBoundaries.ExampleModule),
            current.ModuleLibraryFileName);
        Assert.Equal(OperateAbi.Version, current.OperateVersion);
        Assert.Equal(OperateAbi.Entrypoint, current.OperateEntrypoint);
    }
}
