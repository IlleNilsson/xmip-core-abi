//! Header section 16: an operator's act on a Journey that failed, Retry or
//! Dismiss (`runtime-model.md` section 13; ADR-0013, amendment 2026-08-26).
//!
//! A Journey whose Send Port's every Send Location failed its tries is
//! written Failed and waits in its Port's queue for an operator: Retry sends
//! it again, Dismiss ends it Dismissed. The runtime's library forwards the
//! call to the send step of the node named; a surface over a publication
//! acts through section 14's order. Declarations only; the runtime's tests
//! fail to compile if an export drifts from them.

use crate::ffi::Str;

use super::Scope;

/// `xmip_journey_act_v1`.
pub const JOURNEY_ACT_ENTRYPOINT: &str = "xmip_journey_act_v1";

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
    fn the_entrypoint_matches_the_header() {
        let line = HEADER
            .lines()
            .find(|line| line.starts_with("#define XMIP_JOURNEY_ACT_ENTRYPOINT "))
            .expect("XMIP_JOURNEY_ACT_ENTRYPOINT is in xmip_operate.h");
        assert!(
            line.ends_with(&format!("\"{JOURNEY_ACT_ENTRYPOINT}\"")),
            "{line}"
        );
    }
}
