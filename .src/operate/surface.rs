//! Header section 28.2: a surface, a published snapshot or a node's operate
//! listener, opened as a line and a document state, read, followed and asked
//! to act (ADR-0052, amendment 2026-11-18). Declarations only; the
//! runtime's tests fail to compile if an export drifts from its type.

use crate::ffi::Str;
use crate::operate::publication::Publication;
use crate::operate::remote::SignFn;
use crate::operate::scope_index::ScopeIndex;

/// `xmip_surface_snapshot_v1`: the surface over a published snapshot.
pub const SNAPSHOT_ENTRYPOINT: &str = "xmip_surface_snapshot_v1";
/// `xmip_surface_local_v1`: the surface over a node at its local endpoint.
pub const LOCAL_ENTRYPOINT: &str = "xmip_surface_local_v1";
/// `xmip_surface_remote_v1`: the surface over a node at its operate address.
pub const REMOTE_ENTRYPOINT: &str = "xmip_surface_remote_v1";
/// `xmip_surface_remote_kept_v1`: the same, its key a keeper's.
pub const REMOTE_KEPT_ENTRYPOINT: &str = "xmip_surface_remote_kept_v1";
/// `xmip_surface_free_v1`: a surface released.
pub const FREE_ENTRYPOINT: &str = "xmip_surface_free_v1";
/// `xmip_surface_source_v1`: where a surface's answers come from.
pub const SOURCE_ENTRYPOINT: &str = "xmip_surface_source_v1";
/// `xmip_surface_standing_v1`: how a surface stands, nothing asked.
pub const STANDING_ENTRYPOINT: &str = "xmip_surface_standing_v1";
/// `xmip_surface_answers_v1`: whether a surface answers now.
pub const ANSWERS_ENTRYPOINT: &str = "xmip_surface_answers_v1";
/// `xmip_surface_read_v1`: what a surface read, where it changed.
pub const READ_ENTRYPOINT: &str = "xmip_surface_read_v1";
/// `xmip_surface_wait_v1`: a surface followed, told each change.
pub const WAIT_ENTRYPOINT: &str = "xmip_surface_wait_v1";
/// `xmip_surface_act_v1`: an act on one of a node's nouns.
pub const ACT_ENTRYPOINT: &str = "xmip_surface_act_v1";
/// `xmip_surface_act_message_v1`: an act on a failed Message.
pub const ACT_MESSAGE_ENTRYPOINT: &str = "xmip_surface_act_message_v1";
/// `xmip_surface_control_v1`: a scope paused or resumed.
pub const CONTROL_ENTRYPOINT: &str = "xmip_surface_control_v1";
/// `xmip_surface_failed_v1`: the Messages that failed, Port by Port.
pub const FAILED_ENTRYPOINT: &str = "xmip_surface_failed_v1";
/// `xmip_surface_secrets_v1`: the secrets a node keeps.
pub const SECRETS_ENTRYPOINT: &str = "xmip_surface_secrets_v1";
/// `xmip_surface_set_secret_v1`: a secret set through a node.
pub const SET_SECRET_ENTRYPOINT: &str = "xmip_surface_set_secret_v1";
/// `xmip_surface_decided_v1`: the surface a line and a document choose.
pub const DECIDED_ENTRYPOINT: &str = "xmip_surface_decided_v1";
/// `xmip_surface_named_v1`: the files a remote surface presents, checked.
pub const NAMED_ENTRYPOINT: &str = "xmip_surface_named_v1";
/// `xmip_surface_unlisted_v1`: what a list of failed Messages that is none says.
pub const UNLISTED_ENTRYPOINT: &str = "xmip_surface_unlisted_v1";
/// `xmip_surface_port_at_v1`: the node and the Send Port a scope is at.
pub const PORT_AT_ENTRYPOINT: &str = "xmip_surface_port_at_v1";
/// `xmip_surface_remote_refusal_v1`: why `--remote` with no address is refused.
pub const REMOTE_REFUSAL_ENTRYPOINT: &str = "xmip_surface_remote_refusal_v1";
/// `xmip_surface_local_refusal_v1`: why `--local` with no endpoint is refused.
pub const LOCAL_REFUSAL_ENTRYPOINT: &str = "xmip_surface_local_refusal_v1";
/// `xmip_surface_not_acted_v1`: why a snapshot takes no act.
pub const NOT_ACTED_ENTRYPOINT: &str = "xmip_surface_not_acted_v1";
/// `xmip_surface_unnamed_v1`: what a local surface naming no node says.
pub const UNNAMED_ENTRYPOINT: &str = "xmip_surface_unnamed_v1";

/// A surface, opaque: only the runtime knows what is behind it.
#[repr(C)]
pub struct Surface {
    _private: [u8; 0],
}

/// A surface opened over one place: `place` the snapshot's path or the
/// local endpoint (empty: none named), `within` a local call's bound in
/// milliseconds (0: what the node declared), unused by a snapshot.
pub type OpenFn = unsafe extern "C" fn(place: Str, within: u32, out: *mut *mut Surface) -> i32;

/// A surface opened over a node at `address`, presenting the PEM files the
/// three paths name, each empty where none is named.
pub type RemoteFn = unsafe extern "C" fn(
    address: Str,
    certificate: Str,
    key: Str,
    anchors: Str,
    out: *mut *mut Surface,
) -> i32;

/// A surface opened over a node at `address`, presenting the chain at the
/// path `certificate`, whose key the keeper `keeper` names holds and signs
/// with through `sign` and `context`, and trusting the anchors at `anchors`.
pub type RemoteKeptFn = unsafe extern "C" fn(
    address: Str,
    certificate: Str,
    anchors: Str,
    keeper: Str,
    sign: SignFn,
    context: *mut core::ffi::c_void,
    out: *mut *mut Surface,
) -> i32;

/// A surface released, and everything it follows ended.
pub type FreeFn = unsafe extern "C" fn(surface: *mut Surface);

/// A surface asked: `caller` a caller this process holds (0: this side),
/// `input` and `argument` what is asked. `XMIP_OK` with the answer in
/// `out`, its true length in `out_len`; `XMIP_E_INVALID` with the refusal.
pub type AskFn = unsafe extern "C" fn(
    surface: *const Surface,
    caller: u64,
    input: Str,
    argument: Str,
    out: *mut u8,
    cap: usize,
    out_len: *mut usize,
) -> i32;

/// Whether a surface answers now, 1 or 0 in `out`.
pub type AnswersFn = unsafe extern "C" fn(surface: *const Surface, out: *mut u8) -> i32;

/// What a surface read: its head as JSON in `out` and, where the reading is
/// not the one `seen` names, its publication and its scope tree, each the
/// caller's to free.
pub type ReadFn = unsafe extern "C" fn(
    surface: *const Surface,
    seen: u64,
    out_publication: *mut *mut Publication,
    out_index: *mut *mut ScopeIndex,
    out: *mut u8,
    cap: usize,
    out_len: *mut usize,
) -> i32;

/// A surface followed: returns once it has been told more than `seen`
/// times, or `within` milliseconds passed; how many times and the revision
/// last told.
pub type WaitFn = unsafe extern "C" fn(
    surface: *const Surface,
    seen: u64,
    within: u32,
    out_told: *mut u64,
    out_revision: *mut u64,
) -> i32;

/// The shape of the pure exports: section 10's.
pub type SaidFn = super::design::DesignFn;

#[cfg(test)]
mod tests {
    use super::*;

    const HEADER: &str = include_str!("../../include/xmip_operate.h");

    #[test]
    fn every_entrypoint_matches_the_header() {
        for (define, name) in [
            ("SNAPSHOT", SNAPSHOT_ENTRYPOINT),
            ("LOCAL", LOCAL_ENTRYPOINT),
            ("REMOTE", REMOTE_ENTRYPOINT),
            ("REMOTE_KEPT", REMOTE_KEPT_ENTRYPOINT),
            ("FREE", FREE_ENTRYPOINT),
            ("SOURCE", SOURCE_ENTRYPOINT),
            ("ANSWERS", ANSWERS_ENTRYPOINT),
            ("STANDING", STANDING_ENTRYPOINT),
            ("READ", READ_ENTRYPOINT),
            ("WAIT", WAIT_ENTRYPOINT),
            ("ACT", ACT_ENTRYPOINT),
            ("ACT_MESSAGE", ACT_MESSAGE_ENTRYPOINT),
            ("CONTROL", CONTROL_ENTRYPOINT),
            ("FAILED", FAILED_ENTRYPOINT),
            ("SECRETS", SECRETS_ENTRYPOINT),
            ("SET_SECRET", SET_SECRET_ENTRYPOINT),
            ("DECIDED", DECIDED_ENTRYPOINT),
            ("NAMED", NAMED_ENTRYPOINT),
            ("UNLISTED", UNLISTED_ENTRYPOINT),
            ("PORT_AT", PORT_AT_ENTRYPOINT),
            ("REMOTE_REFUSAL", REMOTE_REFUSAL_ENTRYPOINT),
            ("LOCAL_REFUSAL", LOCAL_REFUSAL_ENTRYPOINT),
            ("NOT_ACTED", NOT_ACTED_ENTRYPOINT),
            ("UNNAMED", UNNAMED_ENTRYPOINT),
        ] {
            let define = format!("#define XMIP_SURFACE_{define}_ENTRYPOINT ");
            let line = HEADER
                .lines()
                .find(|line| line.starts_with(&define))
                .unwrap_or_else(|| panic!("{define} is not in xmip_operate.h"));

            assert!(line.ends_with(&format!("\"{name}\"")), "{line}");
        }
        for shape in [
            "XmipSurfaceOpenFn",
            "XmipSurfaceRemoteFn",
            "XmipSurfaceRemoteKeptFn",
            "XmipSurfaceFreeFn",
            "XmipSurfaceAskFn",
            "XmipSurfaceAnswersFn",
            "XmipSurfaceReadFn",
            "XmipSurfaceWaitFn",
        ] {
            assert!(HEADER.contains(shape), "{shape}");
        }
    }
}
