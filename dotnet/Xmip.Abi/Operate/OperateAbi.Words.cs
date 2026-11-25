namespace Xmip.Abi.Operate;

// Section 28.3's symbols: what a surface says and how it orders a list
// (ADR-0052, amendment 2026-11-18). The words are written once, in Rust;
// every .NET surface asks for them by these names and numbers.
public static partial class OperateAbi
{
    /// <summary>A fixed phrase, by <c>XMIP_PHRASE_*</c>.</summary>
    public const string PhraseEntrypoint = "xmip_phrase_v1";

    /// <summary>The word an act crosses to a node as, by <c>XMIP_ACT_*</c>.</summary>
    public const string ActWordEntrypoint = "xmip_act_word_v1";

    /// <summary>What came of an act whose export answered a status.</summary>
    public const string ActOutcomeEntrypoint = "xmip_act_outcome_v1";

    /// <summary>What a status code means.</summary>
    public const string StatusMeaningEntrypoint = "xmip_status_meaning_v1";

    /// <summary>The two boundaries this build speaks.</summary>
    public const string BoundariesEntrypoint = "xmip_boundaries_v1";

    /// <summary>A sentence about a scope, by <c>XMIP_SENTENCE_*</c>.</summary>
    public const string SentenceEntrypoint = "xmip_sentence_v1";

    /// <summary>An act's outcome and the node's words, as a person reads them.</summary>
    public const string ActSaidEntrypoint = "xmip_act_said_v1";

    /// <summary>How long ago a moment was.</summary>
    public const string AgeEntrypoint = "xmip_age_v1";

    /// <summary>A figure, or a dash for none.</summary>
    public const string FigureEntrypoint = "xmip_figure_v1";

    /// <summary>The six figures in one line.</summary>
    public const string FiguresLineEntrypoint = "xmip_figures_line_v1";

    /// <summary>A text as a surface shows it, by <c>XMIP_TEXT_*</c>.</summary>
    public const string TextEntrypoint = "xmip_text_v1";

    /// <summary>Who acts: the name stated, else the user.</summary>
    public const string WhoEntrypoint = "xmip_who_v1";

    /// <summary>The sentence a validation's verdict says.</summary>
    public const string ValidatedEntrypoint = "xmip_validated_v1";

    /// <summary>A run's line, and what it started each node with.</summary>
    public const string RunSaidEntrypoint = "xmip_run_said_v1";

    /// <summary>A node's declaration as a surface says it.</summary>
    public const string CapabilitySaidEntrypoint = "xmip_capability_said_v1";

    /// <summary>A noun's list, chosen and ordered as a query asks.</summary>
    public const string ListQueryEntrypoint = "xmip_list_query_v1";

    /// <summary>The columns a noun's list sorts by.</summary>
    public const string ListColumnsEntrypoint = "xmip_list_columns_v1";

    /// <summary>A word a row of a noun's list shows, by <c>XMIP_LIST_WORD_*</c>.</summary>
    public const string ListWordsEntrypoint = "xmip_list_words_v1";

    /// <summary><c>XMIP_PHRASE_*</c>.</summary>
    public static class Phrase
    {
        /// <summary><c>XMIP_PHRASE_NOTHING_RECORDED</c>.</summary>
        public const int NothingRecorded = 0;
        /// <summary><c>XMIP_PHRASE_NO_AUDIT_DIRECTORY</c>.</summary>
        public const int NoAuditDirectory = 1;
        /// <summary><c>XMIP_PHRASE_NO_WORK_PROCESS</c>.</summary>
        public const int NoWorkProcess = 2;
    }

    /// <summary><c>XMIP_ACT_*</c>, in <c>observe::Act</c>'s order.</summary>
    public static class Act
    {
        /// <summary><c>XMIP_ACT_PAUSE</c>.</summary>
        public const int Pause = 0;
        /// <summary><c>XMIP_ACT_RESUME</c>.</summary>
        public const int Resume = 1;
        /// <summary><c>XMIP_ACT_REMOVE</c>.</summary>
        public const int Remove = 2;
        /// <summary><c>XMIP_ACT_REPLAY</c>.</summary>
        public const int Replay = 3;
        /// <summary><c>XMIP_ACT_RETRY</c>.</summary>
        public const int Retry = 4;
        /// <summary><c>XMIP_ACT_DISMISS</c>.</summary>
        public const int Dismiss = 5;
    }

    /// <summary><c>XMIP_ACTED_*</c>.</summary>
    public static class Outcome
    {
        /// <summary><c>XMIP_ACTED_TAKEN</c>.</summary>
        public const int Taken = 0;
        /// <summary><c>XMIP_ACTED_ASKED</c>.</summary>
        public const int Asked = 1;
        /// <summary><c>XMIP_ACTED_REFUSED</c>.</summary>
        public const int Refused = 2;
    }

    /// <summary><c>XMIP_SENTENCE_*</c>.</summary>
    public static class Sentence
    {
        /// <summary><c>XMIP_SENTENCE_NOTHING_AT</c>.</summary>
        public const int NothingAt = 0;
        /// <summary><c>XMIP_SENTENCE_NOTHING_BENEATH</c>.</summary>
        public const int NothingBeneath = 1;
        /// <summary><c>XMIP_SENTENCE_NOTHING_MEASURED</c>.</summary>
        public const int NothingMeasured = 2;
    }

    /// <summary><c>XMIP_TEXT_*</c>.</summary>
    public static class Text
    {
        /// <summary><c>XMIP_TEXT_VALUE</c>.</summary>
        public const int Value = 0;
        /// <summary><c>XMIP_TEXT_TO_THE_SECOND</c>.</summary>
        public const int ToTheSecond = 1;
    }

    /// <summary><c>XMIP_LIST_*</c>.</summary>
    public static class List
    {
        /// <summary><c>XMIP_LIST_SUBSCRIPTIONS</c>.</summary>
        public const int Subscriptions = 0;
        /// <summary><c>XMIP_LIST_EVENT_SUBSCRIPTIONS</c>.</summary>
        public const int EventSubscriptions = 1;
        /// <summary><c>XMIP_LIST_DEAD_MESSAGES</c>.</summary>
        public const int DeadMessages = 2;
    }

    /// <summary><c>XMIP_LIST_WORD_*</c>.</summary>
    public static class ListWord
    {
        /// <summary><c>XMIP_LIST_WORD_SUBSCRIPTION_SCOPE</c>.</summary>
        public const int SubscriptionScope = 0;
        /// <summary><c>XMIP_LIST_WORD_DEAD_MESSAGE_SCOPE</c>.</summary>
        public const int DeadMessageScope = 1;
        /// <summary><c>XMIP_LIST_WORD_EVENT_WHO</c>.</summary>
        public const int EventWho = 2;
        /// <summary><c>XMIP_LIST_WORD_UNHEARD_LINE</c>.</summary>
        public const int UnheardLine = 3;
    }
}
