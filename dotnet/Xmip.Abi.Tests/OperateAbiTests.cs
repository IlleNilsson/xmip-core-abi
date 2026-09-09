using Xmip.Abi.Operate;

namespace Xmip.Abi.Tests;

/// <summary>
/// The binding agrees with <c>xmip_operate.h</c>: its version, its three
/// symbols, and every health mood and counted kind. ADR-0027 clause 2 makes
/// the version its own; the first test also proves it has not quietly become
/// the module boundary's.
/// </summary>
public sealed class OperateAbiTests
{
    [Fact]
    public void SpeaksTheOperateVersionTheHeaderDeclares()
    {
        Assert.Equal(
            Header.Define("xmip_operate.h", "XMIP_OPERATE_VERSION"),
            $"{OperateAbi.Version}");
    }

    [Theory]
    [InlineData("XMIP_OPERATE_ENTRYPOINT", OperateAbi.Entrypoint)]
    [InlineData("XMIP_START_ENTRYPOINT", OperateAbi.StartEntrypoint)]
    [InlineData("XMIP_VALIDATE_ENTRYPOINT", OperateAbi.ValidateEntrypoint)]
    public void NamesEachSymbolTheHeaderDeclares(string define, string symbol)
    {
        Assert.Equal(Header.Define("xmip_operate.h", define), symbol);
    }

    [Fact]
    public void KnowsEveryHealthMoodTheHeaderDefinesAndNoOther()
    {
        IReadOnlyDictionary<string, int> header = Header.Enumerators("HEALTH");

        Assert.NotEmpty(header);

        foreach ((string constant, int value) in header)
        {
            Assert.Equal(Header.MemberName(constant), ((HealthState)value).ToString());
        }

        Assert.Equal(header.Count, Enum.GetValues<HealthState>().Length);
    }

    [Fact]
    public void KnowsEveryCountedKindTheHeaderDefinesAndNoOther()
    {
        IReadOnlyDictionary<string, int> header = Header.Enumerators("COUNTED");

        Assert.NotEmpty(header);

        foreach ((string constant, int value) in header)
        {
            Assert.Equal(Header.MemberName(constant), ((Counted)value).ToString());
        }

        Assert.Equal(header.Count, Enum.GetValues<Counted>().Length);
    }
}
