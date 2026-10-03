//! Header section 15: a node's Dead Message Queue, seen and replayed
//! (ADR-0052, amendment 2026-10-01).
//!
//! An accepted Message no Subscription matched is kept in the Ledger with
//! its entry in its node's Dead Message Queue; an Operator lists the queue,
//! opens an entry and replays it once a Subscription is added or fixed. The
//! runtime's library forwards every call to its pickup; a surface over a
//! publication replays through section 14's order. Declarations only; the
//! runtime's tests fail to compile if an export drifts from them.

use crate::ffi::Str;

use super::Scope;

/// `xmip_dead_messages_v1`.
pub const DEAD_MESSAGES_ENTRYPOINT: &str = "xmip_dead_messages_v1";
/// `xmip_dead_message_replay_v1`.
pub const DEAD_MESSAGE_REPLAY_ENTRYPOINT: &str = "xmip_dead_message_replay_v1";
/// `xmip_publication_dead_messages_v1`.
pub const PUBLICATION_DEAD_MESSAGES_ENTRYPOINT: &str = "xmip_publication_dead_messages_v1";

/// `xmip_dead_messages_v1`: the Dead Message Queues of every node in this
/// process at or beneath `node` (empty: all), as JSON.
pub type DeadMessagesFn =
    unsafe extern "C" fn(node: Scope, out: *mut u8, cap: usize, out_len: *mut usize) -> i32;

/// `xmip_dead_message_replay_v1`: replay the Message `message` from the
/// Dead Message Queue of the node at `node` in this process, by `who`; what
/// came of it in `said`.
pub type DeadMessageReplayFn = unsafe extern "C" fn(
    node: Scope,
    message: Str,
    who: Str,
    said: *mut u8,
    said_cap: usize,
    said_len: *mut usize,
) -> i32;

/// `xmip_publication_dead_messages_v1`: the Dead Message Queue entries a
/// read publication carries, as JSON.
pub type PublicationDeadMessagesFn = unsafe extern "C" fn(
    publication: *const super::publication::Publication,
    out: *mut u8,
    cap: usize,
    out_len: *mut usize,
) -> i32;

#[cfg(test)]
mod tests {
    use super::*;

    const HEADER: &str = include_str!("../../include/xmip_operate.h");

    #[test]
    fn every_entrypoint_matches_the_header() {
        for (define, name) in [
            ("XMIP_DEAD_MESSAGES_ENTRYPOINT", DEAD_MESSAGES_ENTRYPOINT),
            (
                "XMIP_DEAD_MESSAGE_REPLAY_ENTRYPOINT",
                DEAD_MESSAGE_REPLAY_ENTRYPOINT,
            ),
            (
                "XMIP_PUBLICATION_DEAD_MESSAGES_ENTRYPOINT",
                PUBLICATION_DEAD_MESSAGES_ENTRYPOINT,
            ),
        ] {
            let line = HEADER
                .lines()
                .find(|line| line.starts_with(&format!("#define {define} ")))
                .unwrap_or_else(|| panic!("{define} is not in xmip_operate.h"));

            assert!(line.ends_with(&format!("\"{name}\"")), "{line}");
        }
    }
}
