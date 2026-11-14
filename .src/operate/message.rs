//! Header section 16: the Messages that failed, listed (`runtime-model.md`
//! section 13; ADR-0013, amendment 2026-08-26).
//!
//! A Message whose Send Port's every Send Location failed its tries is
//! written Failed on its Journey and waits in its Port's queue for an
//! operator: Retry sends it again, Dismiss ends it Dismissed, each asked at
//! a node's door through section 17's act and Xmip Storage (ADR-0014,
//! amendment 2026-11-13). Every one is listed at its Port, a page at a time
//! from Xmip Storage, or as a read publication carries them. The runtime's
//! library forwards each call to the send step of the node named.
//! Declarations only; the runtime's tests fail to compile if an export
//! drifts from them.

use crate::ffi::Str;

use super::Scope;

/// `xmip_failed_messages_v1`.
pub const FAILED_MESSAGES_ENTRYPOINT: &str = "xmip_failed_messages_v1";
/// `xmip_publication_failed_messages_v1`.
pub const PUBLICATION_FAILED_MESSAGES_ENTRYPOINT: &str = "xmip_publication_failed_messages_v1";

/// `xmip_failed_messages_v1`: the Messages that failed at the Send Port
/// `port` (empty: every one) of every node in this process at or beneath
/// `node` (empty: all), read from Xmip Storage from the place `from` on, at
/// most `most` of each Port, as JSON.
pub type FailedMessagesFn = unsafe extern "C" fn(
    node: Scope,
    port: Str,
    from: u64,
    most: u32,
    out: *mut u8,
    cap: usize,
    out_len: *mut usize,
) -> i32;

/// `xmip_publication_failed_messages_v1`: the Messages that failed a read
/// publication carries, as JSON.
pub type PublicationFailedMessagesFn = unsafe extern "C" fn(
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
            (
                "XMIP_FAILED_MESSAGES_ENTRYPOINT",
                FAILED_MESSAGES_ENTRYPOINT,
            ),
            (
                "XMIP_PUBLICATION_FAILED_MESSAGES_ENTRYPOINT",
                PUBLICATION_FAILED_MESSAGES_ENTRYPOINT,
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
