//! What a status code means to whoever holds it: the header's name, one
//! line of English, and whether trying again could succeed or the instance
//! is finished — for every code section 3 defines, and the one answer for a
//! code it does not. `xmip status` renders it and `ConvertFrom-XmipStatus`
//! emits it, so no surface judges a code on its own (ADR-0052, amendment
//! 2026-09-24). Until 2026-11-17 the words were the .NET binding's alone.

use crate::ffi::status;

/// What a code is called where it is none the header defines.
pub const UNKNOWN: &str = "unknown";

/// Every code the header defines, its name and its line of English.
const MEANINGS: [(i32, &str, &str); 17] = [
    (status::OK, "Ok", "the call succeeded"),
    (
        status::INVALID,
        "Invalid",
        "an argument was outside its contract",
    ),
    (
        status::UNSUPPORTED,
        "Unsupported",
        "well formed, and not implemented here",
    ),
    (
        status::STATE,
        "State",
        "the wrong lifecycle state for this call",
    ),
    (
        status::NOT_FOUND,
        "NotFound",
        "the thing asked for does not exist",
    ),
    (
        status::MALFORMED,
        "Malformed",
        "not the standard it claims to be",
    ),
    (
        status::CONTRACT,
        "Contract",
        "well formed, and violates the contract",
    ),
    (
        status::TRUNCATED,
        "Truncated",
        "the stream ended mid-structure",
    ),
    (status::IO, "Io", "an input or output fault"),
    (
        status::TIMEOUT,
        "Timeout",
        "the call did not answer in time",
    ),
    (
        status::UNAVAILABLE,
        "Unavailable",
        "the peer refused, or is down",
    ),
    (
        status::AUTH,
        "Auth",
        "authentication or authorization was refused",
    ),
    (
        status::CAPACITY,
        "Capacity",
        "a quota, limit or resource was exhausted",
    ),
    (
        status::CANCELLED,
        "Cancelled",
        "the host asked for cancellation",
    ),
    (status::AGAIN, "Again", "would block; not a failure"),
    (status::INTERNAL, "Internal", "a defect in the module"),
    (
        status::PANIC,
        "Panic",
        "unwinding was caught at the boundary",
    ),
];

/// What one code means.
#[derive(Clone, Debug, PartialEq, Eq)]
pub struct StatusMeaning {
    pub code: i32,
    /// Whether the header defines it.
    pub known: bool,
    /// Its name in the header, `NotFound`; [`UNKNOWN`] for none.
    pub name: &'static str,
    /// One line an operator can read.
    pub meaning: &'static str,
    pub retryable: bool,
    pub terminal: bool,
}

impl StatusMeaning {
    /// What `code` means.
    #[must_use]
    pub fn of(code: i32) -> Self {
        let named = MEANINGS.iter().find(|(known, ..)| *known == code);

        Self {
            code,
            known: named.is_some(),
            name: named.map_or(UNKNOWN, |(_, name, _)| name),
            meaning: explain(code),
            retryable: named.is_some() && status::is_retryable(code),
            terminal: named.is_some() && status::is_terminal(code),
        }
    }
}

/// The header's name of `code`, or `None` for a code it does not define.
#[must_use]
pub fn name(code: i32) -> Option<&'static str> {
    MEANINGS
        .iter()
        .find(|(known, ..)| *known == code)
        .map(|(_, name, _)| *name)
}

/// One line of English for `code`, and one more for a code the header does
/// not define.
#[must_use]
pub fn explain(code: i32) -> &'static str {
    MEANINGS
        .iter()
        .find(|(known, ..)| *known == code)
        .map_or("not a status this build knows", |(.., meaning)| meaning)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn a_known_code_carries_the_header_s_name_and_its_english() {
        let timeout = StatusMeaning::of(status::TIMEOUT);

        assert!(timeout.known);
        assert_eq!(timeout.name, "Timeout");
        assert_eq!(timeout.meaning, explain(status::TIMEOUT));
        assert!(timeout.retryable && !timeout.terminal);
        assert_eq!(name(status::NOT_FOUND), Some("NotFound"));
    }

    #[test]
    fn a_terminal_code_is_never_retryable() {
        let panic = StatusMeaning::of(status::PANIC);

        assert!(panic.terminal && !panic.retryable);
    }

    #[test]
    fn a_code_the_header_does_not_define_is_unknown_in_lower_case_and_nothing_else() {
        let stranger = StatusMeaning::of(4711);

        assert!(!stranger.known);
        assert_eq!((stranger.name, stranger.code), (UNKNOWN, 4711));
        assert!(!stranger.retryable && !stranger.terminal);
        assert_eq!(stranger.meaning, "not a status this build knows");
    }

    #[test]
    fn every_code_the_header_defines_has_a_name_and_a_line() {
        let header = include_str!("../include/xmip_module.h");

        for (code, name, meaning) in MEANINGS {
            assert!(!name.is_empty() && !meaning.is_empty());
            assert!(
                header.contains(&format!("{code}")),
                "{name} ({code}) is not in the header"
            );
        }
    }
}
