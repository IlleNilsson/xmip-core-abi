//! Header sections 17 and 18: a node reached at its operate address — the
//! remote surface, read, followed and acted through over gRPC inside
//! Xmip's mutual TLS — and this process's node served on an operate
//! listener of its own (ADR-0014, amendment 2026-10-12; ADR-0067); and the
//! role a caller holds with the one check every act passes (ADR-0009).
//! Declarations only; the runtime's tests fail to compile if an export
//! drifts from them.

use crate::ffi::Str;

/// `xmip_remote_open_v1`.
pub const OPEN_ENTRYPOINT: &str = "xmip_remote_open_v1";
/// `xmip_remote_publication_v1`.
pub const PUBLICATION_ENTRYPOINT: &str = "xmip_remote_publication_v1";
/// `xmip_remote_wait_v1`.
pub const WAIT_ENTRYPOINT: &str = "xmip_remote_wait_v1";
/// `xmip_remote_act_v1`.
pub const ACT_ENTRYPOINT: &str = "xmip_remote_act_v1";
/// `xmip_remote_end_v1`.
pub const END_ENTRYPOINT: &str = "xmip_remote_end_v1";
/// `xmip_remote_free_v1`.
pub const FREE_ENTRYPOINT: &str = "xmip_remote_free_v1";
/// `xmip_operate_serve_v1`.
pub const SERVE_ENTRYPOINT: &str = "xmip_operate_serve_v1";
/// `xmip_operate_served_address_v1`.
pub const SERVED_ADDRESS_ENTRYPOINT: &str = "xmip_operate_served_address_v1";
/// `xmip_operate_post_v1`.
pub const POST_ENTRYPOINT: &str = "xmip_operate_post_v1";
/// `xmip_operate_serve_stop_v1`.
pub const SERVE_STOP_ENTRYPOINT: &str = "xmip_operate_serve_stop_v1";
/// `xmip_security_role_named_v1`.
pub const SECURITY_ROLE_NAMED_ENTRYPOINT: &str = "xmip_security_role_named_v1";
/// `xmip_security_role_words_v1`.
pub const SECURITY_ROLE_WORDS_ENTRYPOINT: &str = "xmip_security_role_words_v1";
/// `xmip_act_admitted_v1`.
pub const ACT_ADMITTED_ENTRYPOINT: &str = "xmip_act_admitted_v1";

/// A node reached at its operate address, opaque.
#[repr(C)]
pub struct Remote {
    _private: [u8; 0],
}

/// An operate listener this process serves, opaque.
#[repr(C)]
pub struct Served {
    _private: [u8; 0],
}

/// `xmip_remote_open_v1`: the node at `address`, `host:port`, presenting
/// `certificate` with `key` and trusting `anchors`, each PEM.
pub type OpenFn = unsafe extern "C" fn(
    address: Str,
    certificate: Str,
    key: Str,
    anchors: Str,
    out: *mut *mut Remote,
    report: *mut u8,
    cap: usize,
    out_len: *mut usize,
) -> i32;

/// `xmip_remote_publication_v1`: the node's publication and its revision.
pub type PublicationFn = unsafe extern "C" fn(
    remote: *const Remote,
    out: *mut u8,
    cap: usize,
    out_len: *mut usize,
    out_revision: *mut u64,
) -> i32;

/// `xmip_remote_wait_v1`: the follow's state once told more than `seen`
/// times, or after `timeout_ms`.
pub type WaitFn = unsafe extern "C" fn(
    remote: *const Remote,
    seen: u64,
    timeout_ms: u32,
    out_told: *mut u64,
    out_revision: *mut u64,
    out_reachable: *mut u8,
    reason: *mut u8,
    cap: usize,
    reason_len: *mut usize,
) -> i32;

/// `xmip_remote_act_v1`: `act` on the `noun` called `target` of the node
/// at `node`.
pub type ActFn = unsafe extern "C" fn(
    remote: *const Remote,
    node: Str,
    noun: Str,
    target: Str,
    act: Str,
    said: *mut u8,
    cap: usize,
    said_len: *mut usize,
) -> i32;

/// `xmip_remote_end_v1`: the follow ended and every wait woken.
pub type EndFn = unsafe extern "C" fn(remote: *const Remote);

/// `xmip_remote_free_v1`.
pub type FreeFn = unsafe extern "C" fn(remote: *mut Remote);

/// `xmip_operate_serve_v1`: this process's node served at `listen`.
pub type ServeFn = unsafe extern "C" fn(
    listen: Str,
    certificate: Str,
    key: Str,
    anchors: Str,
    role: Str,
    program: Str,
    out: *mut *mut Served,
    report: *mut u8,
    cap: usize,
    out_len: *mut usize,
) -> i32;

/// `xmip_operate_served_address_v1`: where it listens.
pub type ServedAddressFn = unsafe extern "C" fn(
    served: *const Served,
    out: *mut u8,
    cap: usize,
    out_len: *mut usize,
) -> i32;

/// `xmip_operate_post_v1`: what the listener answers from now.
pub type PostFn = unsafe extern "C" fn(served: *const Served, publication: Str) -> i32;

/// `xmip_operate_serve_stop_v1`.
pub type ServeStopFn = unsafe extern "C" fn(served: *mut Served);

/// `xmip_security_role_named_v1`: the role a word names.
pub type SecurityRoleNamedFn = unsafe extern "C" fn(word: Str, out: *mut i32) -> i32;

/// `xmip_security_role_words_v1`: what a role is called and may do.
pub type SecurityRoleWordsFn =
    unsafe extern "C" fn(role: i32, out_word: *mut Str, out_description: *mut Str) -> i32;

/// `xmip_act_admitted_v1`: whether a caller holding a role may act.
pub type ActAdmittedFn = unsafe extern "C" fn(
    who: Str,
    proven: u8,
    role: i32,
    act: Str,
    target: Str,
    said: *mut u8,
    cap: usize,
    said_len: *mut usize,
) -> i32;

#[cfg(test)]
mod tests {
    use super::*;

    const HEADER: &str = include_str!("../../include/xmip_operate.h");

    #[test]
    fn every_entrypoint_matches_the_header() {
        for (define, name) in [
            ("XMIP_REMOTE_OPEN_ENTRYPOINT", OPEN_ENTRYPOINT),
            ("XMIP_REMOTE_PUBLICATION_ENTRYPOINT", PUBLICATION_ENTRYPOINT),
            ("XMIP_REMOTE_WAIT_ENTRYPOINT", WAIT_ENTRYPOINT),
            ("XMIP_REMOTE_ACT_ENTRYPOINT", ACT_ENTRYPOINT),
            ("XMIP_REMOTE_END_ENTRYPOINT", END_ENTRYPOINT),
            ("XMIP_REMOTE_FREE_ENTRYPOINT", FREE_ENTRYPOINT),
            ("XMIP_OPERATE_SERVE_ENTRYPOINT", SERVE_ENTRYPOINT),
            (
                "XMIP_OPERATE_SERVED_ADDRESS_ENTRYPOINT",
                SERVED_ADDRESS_ENTRYPOINT,
            ),
            ("XMIP_OPERATE_POST_ENTRYPOINT", POST_ENTRYPOINT),
            ("XMIP_OPERATE_SERVE_STOP_ENTRYPOINT", SERVE_STOP_ENTRYPOINT),
            (
                "XMIP_SECURITY_ROLE_NAMED_ENTRYPOINT",
                SECURITY_ROLE_NAMED_ENTRYPOINT,
            ),
            (
                "XMIP_SECURITY_ROLE_WORDS_ENTRYPOINT",
                SECURITY_ROLE_WORDS_ENTRYPOINT,
            ),
            ("XMIP_ACT_ADMITTED_ENTRYPOINT", ACT_ADMITTED_ENTRYPOINT),
        ] {
            let line = HEADER
                .lines()
                .find(|line| line.starts_with(&format!("#define {define} ")))
                .unwrap_or_else(|| panic!("{define} is not in xmip_operate.h"));

            assert!(line.ends_with(&format!("\"{name}\"")), "{line}");
        }
    }
}
