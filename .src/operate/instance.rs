//! Header section 29: a Work Process's Instances, listed (`runtime-model.md`
//! section 22; ADR-0066, amendment 2026-11-25).
//!
//! Each run of a Work Process is an Instance, holding no thread while it
//! waits for a Message of its key or a time. Every one is listed at its
//! Work Process — running, waiting, failed, dismissed or completed, its own
//! values and its history of Messages — beside the Messages that failed in
//! its queue, from the Ledger or as a read publication carries them. A
//! Message that failed there takes section 16's acts. The runtime's library
//! forwards each call to the Work Process step of the node named.
//! Declarations only; the runtime's tests fail to compile if an export
//! drifts from them.

use crate::ffi::Str;

use super::Scope;

/// `xmip_instances_v1`.
pub const INSTANCES_ENTRYPOINT: &str = "xmip_instances_v1";
/// `xmip_publication_instances_v1`.
pub const PUBLICATION_INSTANCES_ENTRYPOINT: &str = "xmip_publication_instances_v1";

/// `xmip_instances_v1`: the Instances of the Work Process `work_process`
/// (empty: every one) of every node in this process at or beneath `node`
/// (empty: all), read from the Ledger, newest written first, at most `most`
/// of each, and the Messages that failed in its queue, as JSON.
pub type InstancesFn = unsafe extern "C" fn(
    node: Scope,
    work_process: Str,
    most: u32,
    out: *mut u8,
    cap: usize,
    out_len: *mut usize,
) -> i32;

/// `xmip_publication_instances_v1`: the Instances a read publication
/// carries, as JSON.
pub type PublicationInstancesFn = unsafe extern "C" fn(
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
            ("XMIP_INSTANCES_ENTRYPOINT", INSTANCES_ENTRYPOINT),
            (
                "XMIP_PUBLICATION_INSTANCES_ENTRYPOINT",
                PUBLICATION_INSTANCES_ENTRYPOINT,
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
