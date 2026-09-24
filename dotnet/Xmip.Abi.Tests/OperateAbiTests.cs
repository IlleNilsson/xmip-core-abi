using Xmip.Abi.Operate;

namespace Xmip.Abi.Tests;

/// <summary>
/// The binding agrees with <c>xmip_operate.h</c>: its version, its
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
    [InlineData("XMIP_SCOPE_CONTAINS_ENTRYPOINT", OperateAbi.ScopeContainsEntrypoint)]
    [InlineData("XMIP_SCOPE_PARTS_ENTRYPOINT", OperateAbi.ScopePartsEntrypoint)]
    [InlineData("XMIP_STAGE_WORDS_ENTRYPOINT", OperateAbi.StageWordsEntrypoint)]
    [InlineData("XMIP_STAGE_DECLARED_ENTRYPOINT", OperateAbi.StageDeclaredEntrypoint)]
    [InlineData("XMIP_HEALTH_WORD_ENTRYPOINT", OperateAbi.HealthWordEntrypoint)]
    [InlineData("XMIP_HEALTH_COLOR_ENTRYPOINT", OperateAbi.HealthColorEntrypoint)]
    [InlineData("XMIP_HEALTH_NAMED_ENTRYPOINT", OperateAbi.HealthNamedEntrypoint)]
    [InlineData("XMIP_HEALTH_ORDER_ENTRYPOINT", OperateAbi.HealthOrderEntrypoint)]
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
