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
    [InlineData("XMIP_HEALTH_ROLLED_ENTRYPOINT", OperateAbi.HealthRolledEntrypoint)]
    [InlineData("XMIP_COUNTED_WORD_ENTRYPOINT", OperateAbi.CountedWordEntrypoint)]
    [InlineData("XMIP_STAGE_COUNTED_ENTRYPOINT", OperateAbi.StageCountedEntrypoint)]
    [InlineData("XMIP_STAGE_PAUSABLE_ENTRYPOINT", OperateAbi.StagePausableEntrypoint)]
    [InlineData("XMIP_STAGE_LOCATION_ENTRYPOINT", OperateAbi.StageLocationEntrypoint)]
    [InlineData(
        "XMIP_CAPABILITY_PUBLISHED_ENTRYPOINT", OperateAbi.CapabilityPublishedEntrypoint)]
    [InlineData("XMIP_CAPABILITY_ENTRY_ENTRYPOINT", OperateAbi.CapabilityEntryEntrypoint)]
    [InlineData("XMIP_PUBLICATION_READ_ENTRYPOINT", OperateAbi.PublicationReadEntrypoint)]
    [InlineData("XMIP_PUBLICATION_FREE_ENTRYPOINT", OperateAbi.PublicationFreeEntrypoint)]
    [InlineData("XMIP_PUBLICATION_HEAD_ENTRYPOINT", OperateAbi.PublicationHeadEntrypoint)]
    [InlineData("XMIP_PUBLICATION_RECORDS_ENTRYPOINT", OperateAbi.PublicationRecordsEntrypoint)]
    [InlineData("XMIP_PUBLICATION_COUNTS_ENTRYPOINT", OperateAbi.PublicationCountsEntrypoint)]
    [InlineData("XMIP_PUBLICATION_NODES_ENTRYPOINT", OperateAbi.PublicationNodesEntrypoint)]
    [InlineData("XMIP_PUBLICATION_LINKS_ENTRYPOINT", OperateAbi.PublicationLinksEntrypoint)]
    [InlineData("XMIP_PUBLICATION_RUN_ENTRYPOINT", OperateAbi.PublicationRunEntrypoint)]
    [InlineData("XMIP_CURVE_READ_ENTRYPOINT", OperateAbi.CurveReadEntrypoint)]
    [InlineData("XMIP_CURVE_POINTS_ENTRYPOINT", OperateAbi.CurvePointsEntrypoint)]
    [InlineData("XMIP_CURVE_FREE_ENTRYPOINT", OperateAbi.CurveFreeEntrypoint)]
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

    [Theory]
    [InlineData("TOPOLOGY", typeof(TopologyNodeKind))]
    [InlineData("ORIGIN", typeof(TopologyOrigin))]
    [InlineData("PATTERN", typeof(CommunicationPattern))]
    [InlineData("RUN", typeof(RunList))]
    public void KnowsEverySectionEightValueTheHeaderDefinesAndNoOther(string family, Type values)
    {
        IReadOnlyDictionary<string, int> header = Header.Enumerators(family);

        Assert.NotEmpty(header);

        foreach ((string constant, int value) in header)
        {
            Assert.Equal(Header.MemberName(constant), Enum.GetName(values, value));
        }

        Assert.Equal(header.Count, Enum.GetValues(values).Length);
    }
}
