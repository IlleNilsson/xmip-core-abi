using System.Runtime.InteropServices;
using Xmip.Abi.Operate;

namespace Xmip.Abi.Test;

/// <summary>
/// Section 28.1's symbols, words and layouts agree with
/// <c>xmip_operate.h</c>: a publication's scope tree (ADR-0052, amendment
/// 2026-11-17).
/// </summary>
public sealed class OperateAbiScopeIndexTest
{
    [Theory]
    [InlineData("XMIP_SCOPE_INDEX_BUILD_ENTRYPOINT", OperateAbi.ScopeIndexBuildEntrypoint)]
    [InlineData(
        "XMIP_SCOPE_INDEX_OF_PUBLICATION_ENTRYPOINT", OperateAbi.ScopeIndexOfPublicationEntrypoint)]
    [InlineData("XMIP_SCOPE_INDEX_FREE_ENTRYPOINT", OperateAbi.ScopeIndexFreeEntrypoint)]
    [InlineData("XMIP_SCOPE_INDEX_FACTS_ENTRYPOINT", OperateAbi.ScopeIndexFactsEntrypoint)]
    [InlineData("XMIP_SCOPE_INDEX_PLACED_ENTRYPOINT", OperateAbi.ScopeIndexPlacedEntrypoint)]
    [InlineData("XMIP_SCOPE_INDEX_CHILDREN_ENTRYPOINT", OperateAbi.ScopeIndexChildrenEntrypoint)]
    [InlineData("XMIP_SCOPE_INDEX_LIST_ENTRYPOINT", OperateAbi.ScopeIndexListEntrypoint)]
    [InlineData("XMIP_SCOPE_INDEX_LABEL_ENTRYPOINT", OperateAbi.ScopeIndexLabelEntrypoint)]
    [InlineData(
        "XMIP_SCOPE_INDEX_WORST_AT_STAGE_ENTRYPOINT", OperateAbi.ScopeIndexWorstAtStageEntrypoint)]
    [InlineData("XMIP_SCOPE_INDEX_MEASURE_ENTRYPOINT", OperateAbi.ScopeIndexMeasureEntrypoint)]
    [InlineData(
        "XMIP_SCOPE_INDEX_CAPABILITIES_ENTRYPOINT", OperateAbi.ScopeIndexCapabilitiesEntrypoint)]
    [InlineData("XMIP_SCOPE_INDEX_SELECT_ENTRYPOINT", OperateAbi.ScopeIndexSelectEntrypoint)]
    [InlineData("XMIP_SCOPE_RELATIVE_ENTRYPOINT", OperateAbi.ScopeRelativeEntrypoint)]
    [InlineData("XMIP_FIGURES_SUM_ENTRYPOINT", OperateAbi.FiguresSumEntrypoint)]
    public void NamesEachSymbolTheHeaderDeclares(string define, string symbol)
    {
        Assert.Equal(symbol, Header.Define("xmip_operate.h", define));
    }

    [Theory]
    [InlineData("XMIP_SCOPE_LIST_STAGE_SCOPES", OperateAbi.ScopeListStageScopes)]
    [InlineData("XMIP_SCOPE_LIST_NODES", OperateAbi.ScopeListNodes)]
    [InlineData("XMIP_SCOPE_LIST_MATCHING", OperateAbi.ScopeListMatching)]
    [InlineData("XMIP_SCOPE_RELATIVE_PARENT", OperateAbi.ScopeRelativeParent)]
    [InlineData("XMIP_SCOPE_RELATIVE_NODE_SCOPE", OperateAbi.ScopeRelativeNodeScope)]
    [InlineData("XMIP_SCOPE_RELATIVE_NAME", OperateAbi.ScopeRelativeName)]
    public void SaysEachWordAsTheHeaderDoes(string define, uint word)
    {
        Assert.Equal($"{word}", Header.Define("xmip_operate.h", define));
    }

    [Fact]
    public void LaysEachStructOutAsTheHeaderDoes()
    {
        int text = Marshal.SizeOf<Module.XmipStr>();

        // Two strings, a pointer and a length, an i64, two i32, four bytes
        // padded to eight, a string and six u64.
        Assert.Equal((3 * text) + 8 + 8 + 8 + 4 + 4 + 8 + 48, Marshal.SizeOf<XmipScopePlaced>());
        Assert.Equal(8 + 8 + 8 + 8 + 8 + text, Marshal.SizeOf<XmipScopeIndexFacts>());
        Assert.Equal(48 + 8 + 8, Marshal.SizeOf<XmipFigures>());
    }
}
