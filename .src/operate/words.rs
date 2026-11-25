//! Header section 28.3: what every surface says and how it orders a list —
//! the sentences, a run's line, a node's declaration, a validation's
//! verdict, the three noun queries and what a status code means (ADR-0052,
//! amendment 2026-11-18). Declarations only; the runtime's tests fail to
//! compile if an export drifts from a type here.

use crate::ffi::Str;

/// `xmip_phrase_v1`: one of the fixed phrases ([`phrase`]), static.
pub const PHRASE_ENTRYPOINT: &str = "xmip_phrase_v1";

/// `xmip_act_word_v1`: the word an act crosses to a node as ([`act`]),
/// static.
pub const ACT_WORD_ENTRYPOINT: &str = "xmip_act_word_v1";

/// `xmip_act_outcome_v1`: what came of an act whose status a node's door
/// answered ([`outcome`]), and its word, static.
pub const ACT_OUTCOME_ENTRYPOINT: &str = "xmip_act_outcome_v1";

/// `xmip_status_meaning_v1`: what a status code means, every text static.
pub const STATUS_MEANING_ENTRYPOINT: &str = "xmip_status_meaning_v1";

/// `xmip_boundaries_v1`: the two boundaries this build speaks, every text
/// static.
pub const BOUNDARIES_ENTRYPOINT: &str = "xmip_boundaries_v1";

/// `xmip_sentence_v1`: one of the sentences about a scope ([`sentence`]).
pub const SENTENCE_ENTRYPOINT: &str = "xmip_sentence_v1";

/// `xmip_act_said_v1`: an act's outcome and the node's words, as a person
/// reads them.
pub const ACT_SAID_ENTRYPOINT: &str = "xmip_act_said_v1";

/// `xmip_age_v1`: how long ago a moment was at another.
pub const AGE_ENTRYPOINT: &str = "xmip_age_v1";

/// `xmip_figure_v1`: a figure as a person reads it, or a dash for none.
pub const FIGURE_ENTRYPOINT: &str = "xmip_figure_v1";

/// `xmip_figures_line_v1`: the six figures in one line.
pub const FIGURES_LINE_ENTRYPOINT: &str = "xmip_figures_line_v1";

/// `xmip_text_v1`: a text as a surface shows it ([`text`]).
pub const TEXT_ENTRYPOINT: &str = "xmip_text_v1";

/// `xmip_who_v1`: who acts — the name stated, else the user.
pub const WHO_ENTRYPOINT: &str = "xmip_who_v1";

/// `xmip_validated_v1`: the sentence a validation's verdict says.
pub const VALIDATED_ENTRYPOINT: &str = "xmip_validated_v1";

/// `xmip_run_said_v1`: a run's line, and what it started each node with.
pub const RUN_SAID_ENTRYPOINT: &str = "xmip_run_said_v1";

/// `xmip_capability_said_v1`: a node's declaration as a surface says it.
pub const CAPABILITY_SAID_ENTRYPOINT: &str = "xmip_capability_said_v1";

/// `xmip_list_query_v1`: a noun's list, chosen and ordered as a query asks.
pub const LIST_QUERY_ENTRYPOINT: &str = "xmip_list_query_v1";

/// `xmip_list_columns_v1`: the columns a noun's list sorts by, static.
pub const LIST_COLUMNS_ENTRYPOINT: &str = "xmip_list_columns_v1";

/// `xmip_list_words_v1`: one word a row of a noun's list shows ([`list_word`]).
pub const LIST_WORDS_ENTRYPOINT: &str = "xmip_list_words_v1";

/// The fixed phrases.
pub mod phrase {
    /// A row with no health recorded.
    pub const NOTHING_RECORDED: i32 = 0;
    /// Why a program stating no audit directory has no audit to read.
    pub const NO_AUDIT_DIRECTORY: i32 = 1;
    /// What the process stage says where it holds Subscriptions alone.
    pub const NO_WORK_PROCESS: i32 = 2;
}

/// The acts an operator takes on a node's nouns, `observe::Act`'s order.
pub mod act {
    pub const PAUSE: i32 = 0;
    pub const RESUME: i32 = 1;
    pub const REMOVE: i32 = 2;
    pub const REPLAY: i32 = 3;
    pub const RETRY: i32 = 4;
    pub const DISMISS: i32 = 5;
}

/// What came of an act.
pub mod outcome {
    pub const TAKEN: i32 = 0;
    pub const ASKED: i32 = 1;
    pub const REFUSED: i32 = 2;
}

/// The sentences about a scope on a surface.
pub mod sentence {
    pub const NOTHING_AT: i32 = 0;
    pub const NOTHING_BENEATH: i32 = 1;
    pub const NOTHING_MEASURED: i32 = 2;
}

/// The texts a surface shows a value as.
pub mod text {
    /// The value, or a dash where it is empty or blank.
    pub const VALUE: i32 = 0;
    /// An RFC 3339 time without its fractional seconds.
    pub const TO_THE_SECOND: i32 = 1;
}

/// A noun's list.
pub mod list {
    pub const SUBSCRIPTIONS: i32 = 0;
    pub const EVENT_SUBSCRIPTIONS: i32 = 1;
    pub const DEAD_MESSAGES: i32 = 2;
}

/// The words a row of a noun's list shows.
pub mod list_word {
    /// A Subscription's scope, from its node and name.
    pub const SUBSCRIPTION_SCOPE: i32 = 0;
    /// A Dead Message Queue entry's scope, from its node and Message.
    pub const DEAD_MESSAGE_SCOPE: i32 = 1;
    /// Who an Event subscription's subscriber is, from its declared name
    /// and its Party.
    pub const EVENT_WHO: i32 = 2;
    /// A member not heard, from the node that does not hear it and what it
    /// says.
    pub const UNHEARD_LINE: i32 = 3;
}

/// What a status code means: static texts.
#[repr(C)]
#[derive(Clone, Copy)]
pub struct StatusMeaning {
    pub known: u8,
    pub name: Str,
    pub meaning: Str,
    pub retryable: u8,
    pub terminal: u8,
}

/// The two boundaries this build speaks: static texts.
#[repr(C)]
#[derive(Clone, Copy)]
pub struct Boundaries {
    pub module_version: u32,
    pub module_entrypoint: Str,
    pub module_library_file_name: Str,
    pub operate_version: u32,
    pub operate_entrypoint: Str,
}

/// `xmip_who_v1`, `xmip_run_said_v1` and `xmip_capability_said_v1` take
/// section 10's shape.
pub type SaidFn = super::design::DesignFn;

/// `xmip_phrase_v1`, `xmip_act_word_v1`: a number in, a static text out.
pub type WordFn = unsafe extern "C" fn(which: i32, out: *mut Str) -> i32;

/// `xmip_act_outcome_v1`.
pub type ActOutcomeFn = unsafe extern "C" fn(status: i32, out: *mut i32, word: *mut Str) -> i32;

/// `xmip_status_meaning_v1`.
pub type StatusMeaningFn = unsafe extern "C" fn(code: i32, out: *mut StatusMeaning) -> i32;

/// `xmip_boundaries_v1`.
pub type BoundariesFn = unsafe extern "C" fn(out: *mut Boundaries) -> i32;

/// `xmip_sentence_v1`, `xmip_list_words_v1`: a number and two texts in, the
/// answer written into `out`, its true length in `out_len`.
pub type SentenceFn = unsafe extern "C" fn(
    which: i32,
    first: Str,
    second: Str,
    out: *mut u8,
    cap: usize,
    out_len: *mut usize,
) -> i32;

/// `xmip_act_said_v1`.
pub type ActSaidFn = unsafe extern "C" fn(
    outcome: i32,
    result: Str,
    out: *mut u8,
    cap: usize,
    out_len: *mut usize,
) -> i32;

/// `xmip_age_v1`.
pub type AgeFn = unsafe extern "C" fn(
    observed: i64,
    now: i64,
    out: *mut u8,
    cap: usize,
    out_len: *mut usize,
) -> i32;

/// `xmip_figure_v1`.
pub type FigureFn =
    unsafe extern "C" fn(has: u8, value: u64, out: *mut u8, cap: usize, out_len: *mut usize) -> i32;

/// `xmip_figures_line_v1`.
pub type FiguresLineFn = unsafe extern "C" fn(
    has: *const u8,
    values: *const u64,
    out: *mut u8,
    cap: usize,
    out_len: *mut usize,
) -> i32;

/// `xmip_text_v1`.
pub type TextFn = unsafe extern "C" fn(
    which: i32,
    text: Str,
    out: *mut u8,
    cap: usize,
    out_len: *mut usize,
) -> i32;

/// `xmip_validated_v1`.
pub type ValidatedFn = unsafe extern "C" fn(
    path: Str,
    status: i32,
    problems: *const Str,
    problems_len: usize,
    out: *mut u8,
    cap: usize,
    out_len: *mut usize,
) -> i32;

/// `xmip_list_query_v1`.
pub type ListQueryFn = unsafe extern "C" fn(
    list: i32,
    entries: Str,
    query: Str,
    out: *mut u8,
    cap: usize,
    out_len: *mut usize,
) -> i32;

/// `xmip_list_columns_v1`.
pub type ListColumnsFn =
    unsafe extern "C" fn(list: i32, out: *mut Str, cap: usize, out_len: *mut usize) -> i32;

#[cfg(test)]
mod tests {
    use super::*;

    const HEADER: &str = include_str!("../../include/xmip_operate.h");

    fn defined(define: &str) -> &'static str {
        HEADER
            .lines()
            .find(|line| line.starts_with(&format!("#define {define} ")))
            .unwrap_or_else(|| panic!("{define} is not in xmip_operate.h"))
    }

    #[test]
    fn every_entrypoint_matches_the_header() {
        for (define, name) in [
            ("XMIP_PHRASE_ENTRYPOINT", PHRASE_ENTRYPOINT),
            ("XMIP_ACT_WORD_ENTRYPOINT", ACT_WORD_ENTRYPOINT),
            ("XMIP_ACT_OUTCOME_ENTRYPOINT", ACT_OUTCOME_ENTRYPOINT),
            ("XMIP_STATUS_MEANING_ENTRYPOINT", STATUS_MEANING_ENTRYPOINT),
            ("XMIP_BOUNDARIES_ENTRYPOINT", BOUNDARIES_ENTRYPOINT),
            ("XMIP_SENTENCE_ENTRYPOINT", SENTENCE_ENTRYPOINT),
            ("XMIP_ACT_SAID_ENTRYPOINT", ACT_SAID_ENTRYPOINT),
            ("XMIP_AGE_ENTRYPOINT", AGE_ENTRYPOINT),
            ("XMIP_FIGURE_ENTRYPOINT", FIGURE_ENTRYPOINT),
            ("XMIP_FIGURES_LINE_ENTRYPOINT", FIGURES_LINE_ENTRYPOINT),
            ("XMIP_TEXT_ENTRYPOINT", TEXT_ENTRYPOINT),
            ("XMIP_WHO_ENTRYPOINT", WHO_ENTRYPOINT),
            ("XMIP_VALIDATED_ENTRYPOINT", VALIDATED_ENTRYPOINT),
            ("XMIP_RUN_SAID_ENTRYPOINT", RUN_SAID_ENTRYPOINT),
            (
                "XMIP_CAPABILITY_SAID_ENTRYPOINT",
                CAPABILITY_SAID_ENTRYPOINT,
            ),
            ("XMIP_LIST_QUERY_ENTRYPOINT", LIST_QUERY_ENTRYPOINT),
            ("XMIP_LIST_COLUMNS_ENTRYPOINT", LIST_COLUMNS_ENTRYPOINT),
            ("XMIP_LIST_WORDS_ENTRYPOINT", LIST_WORDS_ENTRYPOINT),
        ] {
            assert!(
                defined(define).ends_with(&format!("\"{name}\"")),
                "{define}"
            );
        }
    }

    #[test]
    fn every_number_matches_the_header() {
        for (define, value) in [
            ("XMIP_PHRASE_NOTHING_RECORDED", phrase::NOTHING_RECORDED),
            ("XMIP_PHRASE_NO_AUDIT_DIRECTORY", phrase::NO_AUDIT_DIRECTORY),
            ("XMIP_PHRASE_NO_WORK_PROCESS", phrase::NO_WORK_PROCESS),
            ("XMIP_ACT_PAUSE", act::PAUSE),
            ("XMIP_ACT_RESUME", act::RESUME),
            ("XMIP_ACT_REMOVE", act::REMOVE),
            ("XMIP_ACT_REPLAY", act::REPLAY),
            ("XMIP_ACT_RETRY", act::RETRY),
            ("XMIP_ACT_DISMISS", act::DISMISS),
            ("XMIP_ACTED_TAKEN", outcome::TAKEN),
            ("XMIP_ACTED_ASKED", outcome::ASKED),
            ("XMIP_ACTED_REFUSED", outcome::REFUSED),
            ("XMIP_SENTENCE_NOTHING_AT", sentence::NOTHING_AT),
            ("XMIP_SENTENCE_NOTHING_BENEATH", sentence::NOTHING_BENEATH),
            ("XMIP_SENTENCE_NOTHING_MEASURED", sentence::NOTHING_MEASURED),
            ("XMIP_TEXT_VALUE", text::VALUE),
            ("XMIP_TEXT_TO_THE_SECOND", text::TO_THE_SECOND),
            ("XMIP_LIST_SUBSCRIPTIONS", list::SUBSCRIPTIONS),
            ("XMIP_LIST_EVENT_SUBSCRIPTIONS", list::EVENT_SUBSCRIPTIONS),
            ("XMIP_LIST_DEAD_MESSAGES", list::DEAD_MESSAGES),
            (
                "XMIP_LIST_WORD_SUBSCRIPTION_SCOPE",
                list_word::SUBSCRIPTION_SCOPE,
            ),
            (
                "XMIP_LIST_WORD_DEAD_MESSAGE_SCOPE",
                list_word::DEAD_MESSAGE_SCOPE,
            ),
            ("XMIP_LIST_WORD_EVENT_WHO", list_word::EVENT_WHO),
            ("XMIP_LIST_WORD_UNHEARD_LINE", list_word::UNHEARD_LINE),
        ] {
            let line = defined(define);
            let said: i32 = line
                .split_whitespace()
                .nth(2)
                .and_then(|number| number.parse().ok())
                .unwrap_or_else(|| panic!("{line}"));
            assert_eq!(said, value, "{define}");
        }
    }
}
