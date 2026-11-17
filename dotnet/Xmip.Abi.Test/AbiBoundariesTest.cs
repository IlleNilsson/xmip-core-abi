using Xmip.Abi.Module;
using Xmip.Abi.Operate;

namespace Xmip.Abi.Test;

/// <summary>
/// The two boundaries <c>xmip abi</c> and <c>Get-XmipAbi</c> report, each
/// the binding's own constants.
/// </summary>
public sealed class AbiBoundariesTest
{
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
