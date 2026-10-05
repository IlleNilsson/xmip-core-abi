//! Header section 16: the Journeys that failed, listed, and an operator's act
//! on one, Retry or Dismiss (`runtime-model.md` section 13; ADR-0013,
//! amendment 2026-08-26).
//!
//! A Journey whose Send Port's every Send Location failed its tries is
//! written Failed and waits in its Port's queue for an operator: Retry sends
//! it again, Dismiss ends it Dismissed. Every one is listed at its Port, a
//! page at a time from Xmip Storage, or as a read publication carries them.
//! The runtime's library forwards each call to the send step of the node
//! named; a surface over a publication acts through section 14's order. Declarations only; the runtime's tests
//! fail to compile if an export drifts from them.

use crate::ffi::Str;

use super::Scope;

/// `xmip_journey_act_v1`.
pub const JOURNEY_ACT_ENTRYPOINT: &str = "xmip_journey_act_v1";
/// `xmip_failed_journeys_v1`.
pub const FAILED_JOURNEYS_ENTRYPOINT: &str = "xmip_failed_journeys_v1";
/// `xmip_publication_failed_journeys_v1`.
pub const PUBLICATION_FAILED_JOURNEYS_ENTRYPOINT: &str = "xmip_publication_failed_journeys_v1";

/// `xmip_failed_journeys_v1`: the Journeys that failed at the Send Port
/// `port` (empty: every one) of every node in this process at or beneath
/// `node` (empty: all), read from Xmip Storage from the place `from` on, at
/// most `most` of each Port, as JSON.
pub type FailedJourneysFn = unsafe extern "C" fn(
    node: Scope,
    port: Str,
    from: u64,
    most: u32,
    out: *mut u8,
    cap: usize,
    out_len: *mut usize,
) -> i32;

/// `xmip_publication_failed_journeys_v1`: the Journeys that failed a read
/// publication carries, as JSON.
pub type PublicationFailedJourneysFn = unsafe extern "C" fn(
    publication: *const super::publication::Publication,
    out: *mut u8,
    cap: usize,
    out_len: *mut usize,
) -> i32;

/// `xmip_journey_act_v1`: apply `act` — retry or dismiss — to the Journey
/// `journey` sent by the node at `node` in this process, by `who`; what
/// came of it in `said`.
pub type JourneyActFn = unsafe extern "C" fn(
    node: Scope,
    journey: Str,
    act: Str,
    who: Str,
    said: *mut u8,
    said_cap: usize,
    said_len: *mut usize,
) -> i32;

#[cfg(test)]
mod tests {
    use super::*;

    const HEADER: &str = include_str!("../../include/xmip_operate.h");

    #[test]
    fn every_entrypoint_matches_the_header() {
        for (define, name) in [
            ("XMIP_JOURNEY_ACT_ENTRYPOINT", JOURNEY_ACT_ENTRYPOINT),
            (
                "XMIP_FAILED_JOURNEYS_ENTRYPOINT",
                FAILED_JOURNEYS_ENTRYPOINT,
            ),
            (
                "XMIP_PUBLICATION_FAILED_JOURNEYS_ENTRYPOINT",
                PUBLICATION_FAILED_JOURNEYS_ENTRYPOINT,
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
