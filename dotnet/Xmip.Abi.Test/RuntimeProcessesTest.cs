using Xmip.Abi.Module;
using Xmip.Abi.Operate;

namespace Xmip.Abi.Test;

/// <summary>
/// <see cref="RuntimeProcesses"/> crosses section 13 of <c>xmip_operate.h</c>
/// to the runtime this estate built. The declaration, its file and its reading
/// are tested in Rust, where they are written (<c>xmip-core-node</c>); these
/// prove the crossing: a declaration made from .NET is read back whole, and a
/// refusal is carried as the runtime's sentence.
/// </summary>
public sealed class RuntimeProcessesTest
{
    [Fact]
    public void ADeclarationMadeFromDotNetIsReadBackWhole()
    {
        RuntimeProcesses processes = RuntimeRulesTest.Rules.Processes;
        string location = TestCluster.Read().Scope;

        XmipStatus status = processes.Declare(
            "xmip-abi-test", location, "test", [new("stress", "calm")], out string file);

        Assert.True(status == XmipStatus.Ok, file);

        try
        {
            ProcessDeclarations read = processes.Read(Path.GetDirectoryName(file));
            ProcessStanding standing = Assert.Single(
                read.Processes, process => process.File == file);

            Assert.Equal("xmip-abi-test", standing.Name);
            Assert.Equal(location, standing.Location);
            Assert.Equal("test", standing.Purpose);
            Assert.Equal(Environment.ProcessId, standing.Pid);
            Assert.Equal("calm", standing.Said["stress"]);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void APurposeThatIsNoWordIsRefusedInTheRuntimesWords()
    {
        Assert.Equal(
            XmipStatus.Invalid,
            RuntimeRulesTest.Rules.Processes.Declare(
                "xmip-abi-test", string.Empty, "production", null, out string refusal));
        Assert.StartsWith("REFUSED", refusal, StringComparison.Ordinal);
    }
}
