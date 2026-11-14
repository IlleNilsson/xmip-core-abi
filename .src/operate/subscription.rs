//! Header section 14: a node's Subscriptions, seen (ADR-0013, amendment
//! 2026-09-30).
//!
//! A Subscription picks a published Message up and opens a Journey; it is
//! configuration, drawn in an Xmip Application and bound in a node's TOML,
//! and added and removed there. An operator lists them here, and pauses or
//! resumes one at a node's door, through section 17's act and Xmip Storage
//! (ADR-0014, amendment 2026-11-13). The runtime's library forwards every
//! call to its pickup; each language binds these shapes once. Declarations
//! only; the runtime's tests fail to compile if an export drifts from them.

use super::Scope;

/// `xmip_subscriptions_v1`.
pub const SUBSCRIPTIONS_ENTRYPOINT: &str = "xmip_subscriptions_v1";
/// `xmip_publication_subscriptions_v1`.
pub const PUBLICATION_SUBSCRIPTIONS_ENTRYPOINT: &str = "xmip_publication_subscriptions_v1";

/// `xmip_subscriptions_v1`: the Subscriptions of every node in this process
/// at or beneath `node` (empty: all), as JSON.
pub type SubscriptionsFn =
    unsafe extern "C" fn(node: Scope, out: *mut u8, cap: usize, out_len: *mut usize) -> i32;

/// `xmip_publication_subscriptions_v1`: the Subscriptions a read publication
/// carries, as JSON.
pub type PublicationSubscriptionsFn = unsafe extern "C" fn(
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
            ("XMIP_SUBSCRIPTIONS_ENTRYPOINT", SUBSCRIPTIONS_ENTRYPOINT),
            (
                "XMIP_PUBLICATION_SUBSCRIPTIONS_ENTRYPOINT",
                PUBLICATION_SUBSCRIPTIONS_ENTRYPOINT,
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
