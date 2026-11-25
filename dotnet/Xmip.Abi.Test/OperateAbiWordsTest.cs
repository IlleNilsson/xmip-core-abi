using System.Runtime.InteropServices;
using Xmip.Abi.Operate;

namespace Xmip.Abi.Test;

/// <summary>
/// Section 28.3's symbols, numbers and layouts agree with
/// <c>xmip_operate.h</c>: what a surface says and how it orders a list
/// (ADR-0052, amendment 2026-11-18).
/// </summary>
public sealed class OperateAbiWordsTest
{
    [Theory]
    [InlineData("XMIP_PHRASE_ENTRYPOINT", OperateAbi.PhraseEntrypoint)]
    [InlineData("XMIP_ACT_WORD_ENTRYPOINT", OperateAbi.ActWordEntrypoint)]
    [InlineData("XMIP_ACT_OUTCOME_ENTRYPOINT", OperateAbi.ActOutcomeEntrypoint)]
    [InlineData("XMIP_STATUS_MEANING_ENTRYPOINT", OperateAbi.StatusMeaningEntrypoint)]
    [InlineData("XMIP_BOUNDARIES_ENTRYPOINT", OperateAbi.BoundariesEntrypoint)]
    [InlineData("XMIP_SENTENCE_ENTRYPOINT", OperateAbi.SentenceEntrypoint)]
    [InlineData("XMIP_ACT_SAID_ENTRYPOINT", OperateAbi.ActSaidEntrypoint)]
    [InlineData("XMIP_AGE_ENTRYPOINT", OperateAbi.AgeEntrypoint)]
    [InlineData("XMIP_FIGURE_ENTRYPOINT", OperateAbi.FigureEntrypoint)]
    [InlineData("XMIP_FIGURES_LINE_ENTRYPOINT", OperateAbi.FiguresLineEntrypoint)]
    [InlineData("XMIP_TEXT_ENTRYPOINT", OperateAbi.TextEntrypoint)]
    [InlineData("XMIP_WHO_ENTRYPOINT", OperateAbi.WhoEntrypoint)]
    [InlineData("XMIP_VALIDATED_ENTRYPOINT", OperateAbi.ValidatedEntrypoint)]
    [InlineData("XMIP_RUN_SAID_ENTRYPOINT", OperateAbi.RunSaidEntrypoint)]
    [InlineData("XMIP_CAPABILITY_SAID_ENTRYPOINT", OperateAbi.CapabilitySaidEntrypoint)]
    [InlineData("XMIP_LIST_QUERY_ENTRYPOINT", OperateAbi.ListQueryEntrypoint)]
    [InlineData("XMIP_LIST_COLUMNS_ENTRYPOINT", OperateAbi.ListColumnsEntrypoint)]
    [InlineData("XMIP_LIST_WORDS_ENTRYPOINT", OperateAbi.ListWordsEntrypoint)]
    public void NamesEachSymbolTheHeaderDeclares(string define, string symbol)
    {
        Assert.Equal(symbol, Header.Define("xmip_operate.h", define));
    }

    [Theory]
    [InlineData("XMIP_PHRASE_NOTHING_RECORDED", OperateAbi.Phrase.NothingRecorded)]
    [InlineData("XMIP_PHRASE_NO_AUDIT_DIRECTORY", OperateAbi.Phrase.NoAuditDirectory)]
    [InlineData("XMIP_PHRASE_NO_WORK_PROCESS", OperateAbi.Phrase.NoWorkProcess)]
    [InlineData("XMIP_ACT_PAUSE", OperateAbi.Act.Pause)]
    [InlineData("XMIP_ACT_RESUME", OperateAbi.Act.Resume)]
    [InlineData("XMIP_ACT_REMOVE", OperateAbi.Act.Remove)]
    [InlineData("XMIP_ACT_REPLAY", OperateAbi.Act.Replay)]
    [InlineData("XMIP_ACT_RETRY", OperateAbi.Act.Retry)]
    [InlineData("XMIP_ACT_DISMISS", OperateAbi.Act.Dismiss)]
    [InlineData("XMIP_ACTED_TAKEN", OperateAbi.Outcome.Taken)]
    [InlineData("XMIP_ACTED_ASKED", OperateAbi.Outcome.Asked)]
    [InlineData("XMIP_ACTED_REFUSED", OperateAbi.Outcome.Refused)]
    [InlineData("XMIP_SENTENCE_NOTHING_AT", OperateAbi.Sentence.NothingAt)]
    [InlineData("XMIP_SENTENCE_NOTHING_BENEATH", OperateAbi.Sentence.NothingBeneath)]
    [InlineData("XMIP_SENTENCE_NOTHING_MEASURED", OperateAbi.Sentence.NothingMeasured)]
    [InlineData("XMIP_TEXT_VALUE", OperateAbi.Text.Value)]
    [InlineData("XMIP_TEXT_TO_THE_SECOND", OperateAbi.Text.ToTheSecond)]
    [InlineData("XMIP_LIST_SUBSCRIPTIONS", OperateAbi.List.Subscriptions)]
    [InlineData("XMIP_LIST_EVENT_SUBSCRIPTIONS", OperateAbi.List.EventSubscriptions)]
    [InlineData("XMIP_LIST_DEAD_MESSAGES", OperateAbi.List.DeadMessages)]
    [InlineData("XMIP_LIST_WORD_SUBSCRIPTION_SCOPE", OperateAbi.ListWord.SubscriptionScope)]
    [InlineData("XMIP_LIST_WORD_DEAD_MESSAGE_SCOPE", OperateAbi.ListWord.DeadMessageScope)]
    [InlineData("XMIP_LIST_WORD_EVENT_WHO", OperateAbi.ListWord.EventWho)]
    [InlineData("XMIP_LIST_WORD_UNHEARD_LINE", OperateAbi.ListWord.UnheardLine)]
    public void SaysEachNumberAsTheHeaderDoes(string define, int number)
    {
        Assert.Equal($"{number}", Header.Define("xmip_operate.h", define));
    }

    [Fact]
    public void LaysEachStructOutAsTheHeaderDoes()
    {
        int text = Marshal.SizeOf<Module.XmipStr>();

        // A byte padded to eight, two strings, two bytes padded to eight.
        Assert.Equal(8 + (2 * text) + 8, Marshal.SizeOf<XmipStatusMeaning>());

        // A u32 padded to eight, two strings, a u32 padded to eight, a string.
        Assert.Equal(8 + (2 * text) + 8 + text, Marshal.SizeOf<XmipBoundaries>());
    }
}
