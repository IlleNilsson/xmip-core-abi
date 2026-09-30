//! Header section 14: a node's Subscriptions, seen and paused, and an
//! operator's order left for a node (ADR-0013, amendment 2026-09-30).
//!
//! A Subscription picks a published Message up and opens a Journey; it is
//! configuration, drawn in an Xmip Application and bound in a node's TOML,
//! and added and removed there. An operator lists them and pauses or
//! resumes one — never removes one. The runtime's library forwards every
//! call to its pickup and to `observe`'s order; each language binds these
//! shapes once. Declarations only; the runtime's tests fail to compile if an
//! export drifts from them.

use crate::ffi::Str;

use super::Scope;

/// `xmip_subscriptions_v1`.
pub const SUBSCRIPTIONS_ENTRYPOINT: &str = "xmip_subscriptions_v1";
/// `xmip_subscription_act_v1`.
pub const SUBSCRIPTION_ACT_ENTRYPOINT: &str = "xmip_subscription_act_v1";
/// `xmip_publication_subscriptions_v1`.
pub const PUBLICATION_SUBSCRIPTIONS_ENTRYPOINT: &str = "xmip_publication_subscriptions_v1";
/// `xmip_order_v1`.
pub const ORDER_ENTRYPOINT: &str = "xmip_order_v1";

/// `xmip_subscriptions_v1`: the Subscriptions of every node in this process
/// at or beneath `node` (empty: all), as JSON.
pub type SubscriptionsFn =
    unsafe extern "C" fn(node: Scope, out: *mut u8, cap: usize, out_len: *mut usize) -> i32;

/// `xmip_subscription_act_v1`: pause or resume the Subscription `name` of
/// the node at `node` in this process, by `who`; what came of it in `said`.
pub type SubscriptionActFn = unsafe extern "C" fn(
    node: Scope,
    name: Str,
    act: Str,
    who: Str,
    said: *mut u8,
    said_cap: usize,
    said_len: *mut usize,
) -> i32;

/// `xmip_publication_subscriptions_v1`: the Subscriptions a read publication
/// carries, as JSON.
pub type PublicationSubscriptionsFn = unsafe extern "C" fn(
    publication: *const super::publication::Publication,
    out: *mut u8,
    cap: usize,
    out_len: *mut usize,
) -> i32;

/// `xmip_order_v1`: `act` on the `noun` called `target` of the node at
/// `node`, left in `orders` for the node to take.
pub type OrderFn = unsafe extern "C" fn(
    orders: Str,
    node: Scope,
    noun: Str,
    target: Str,
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
            ("XMIP_SUBSCRIPTIONS_ENTRYPOINT", SUBSCRIPTIONS_ENTRYPOINT),
            (
                "XMIP_SUBSCRIPTION_ACT_ENTRYPOINT",
                SUBSCRIPTION_ACT_ENTRYPOINT,
            ),
            (
                "XMIP_PUBLICATION_SUBSCRIPTIONS_ENTRYPOINT",
                PUBLICATION_SUBSCRIPTIONS_ENTRYPOINT,
            ),
            ("XMIP_ORDER_ENTRYPOINT", ORDER_ENTRYPOINT),
        ] {
            let line = HEADER
                .lines()
                .find(|line| line.starts_with(&format!("#define {define} ")))
                .unwrap_or_else(|| panic!("{define} is not in xmip_operate.h"));

            assert!(line.ends_with(&format!("\"{name}\"")), "{line}");
        }
    }
}
