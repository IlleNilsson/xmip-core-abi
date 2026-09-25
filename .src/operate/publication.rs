//! Header section 8: a publication read by the runtime, for a surface that
//! reads the file a publisher wrote.
//!
//! A publication's shape is `observe::Publication`'s and nobody else's; a
//! surface hands the runtime the text and gets the records, counts, topology
//! and run back as the header's values (ADR-0027 and ADR-0052, amendments
//! 2026-09-24). Declarations only, as the rest of this crate.

use super::{HealthEntry, Measurement, Scope};
use crate::ffi::Str;

/// `observe::Publication::read`, into a handle.
pub const READ_ENTRYPOINT: &str = "xmip_publication_read_v1";
/// Release a handle.
pub const FREE_ENTRYPOINT: &str = "xmip_publication_free_v1";
/// The publication's head: who, where, and the run's and topology's own.
pub const HEAD_ENTRYPOINT: &str = "xmip_publication_head_v1";
/// Its health records.
pub const RECORDS_ENTRYPOINT: &str = "xmip_publication_records_v1";
/// Its counts.
pub const COUNTS_ENTRYPOINT: &str = "xmip_publication_counts_v1";
/// Its topology's nodes.
pub const NODES_ENTRYPOINT: &str = "xmip_publication_nodes_v1";
/// Its topology's links.
pub const LINKS_ENTRYPOINT: &str = "xmip_publication_links_v1";
/// One of its run's lists.
pub const RUN_ENTRYPOINT: &str = "xmip_publication_run_v1";

/// What a topology kind is called: `observe::NodeKind::word` and `name`.
pub const KIND_WORDS_ENTRYPOINT: &str = "xmip_topology_kind_words_v1";
/// What a topology origin is called: `observe::Origin::word` and `name`.
pub const ORIGIN_WORDS_ENTRYPOINT: &str = "xmip_topology_origin_words_v1";
/// What a communication pattern is called: `observe::Pattern::word` and
/// `name`.
pub const PATTERN_WORDS_ENTRYPOINT: &str = "xmip_topology_pattern_words_v1";

/// A curve (a node's throughput over time), `observe::Curve::read`, into a
/// handle.
pub const CURVE_READ_ENTRYPOINT: &str = "xmip_curve_read_v1";
/// A read curve's points.
pub const CURVE_POINTS_ENTRYPOINT: &str = "xmip_curve_points_v1";
/// Release a curve's handle.
pub const CURVE_FREE_ENTRYPOINT: &str = "xmip_curve_free_v1";

/// Header section 8, `XmipTopologyKind`, as it crosses.
pub mod kind {
    pub const COMPUTER: i32 = 0;
    pub const SERVER: i32 = 1;
    pub const VIRTUAL_MACHINE: i32 = 2;
    pub const GATEWAY: i32 = 3;
    pub const APPLIANCE: i32 = 4;
    pub const SERVICE: i32 = 5;
    pub const PROCESS: i32 = 6;
    pub const INTERFACE: i32 = 7;
    pub const PORT: i32 = 8;
    pub const PROTOCOL: i32 = 9;
    pub const LOCATION: i32 = 10;
    pub const CLUSTER: i32 = 11;
    pub const NODE: i32 = 12;
    pub const STAGE: i32 = 13;
    pub const ENDPOINT: i32 = 14;
}

/// Header section 8, `XmipTopologyOrigin`, as it crosses.
pub mod origin {
    pub const CONFIGURED: i32 = 0;
    pub const OBSERVED: i32 = 1;
    pub const BOTH: i32 = 2;
}

/// Header section 8, `XmipCommunicationPattern`, as it crosses.
pub mod pattern {
    pub const REQUEST_RESPONSE: i32 = 0;
    pub const SEND_RECEIVE: i32 = 1;
    pub const PUBLISH_CONSUME: i32 = 2;
    pub const STREAMING: i32 = 3;
    pub const FIRE_AND_FORGET: i32 = 4;
    pub const SESSION: i32 = 5;
    pub const RETRY: i32 = 6;
}

/// Header section 8, `XmipRunList`, as it crosses.
pub mod run_list {
    pub const TESTS: u32 = 0;
    pub const NODES: u32 = 1;
    pub const CAPABILITIES: u32 = 2;
    pub const ONLINE: u32 = 3;
}

/// A read publication, opaque: only the runtime knows what is behind it.
#[repr(C)]
pub struct Publication {
    _private: [u8; 0],
}

/// A read curve, opaque.
#[repr(C)]
pub struct Curve {
    _private: [u8; 0],
}

/// Header section 8. Who published, where, and the run's and topology's own
/// single values; `has_run` and `has_topology` say whether either was said.
#[repr(C)]
#[derive(Clone, Copy)]
pub struct PublicationHead {
    pub source: Str,
    pub node: Scope,
    pub has_run: u8,
    pub cluster: Str,
    pub stress: Str,
    pub has_topology: u8,
    pub topology_source: Str,
    pub topology_observed_unix_nanos: i64,
}

/// Header section 8. One thing that communicates.
#[repr(C)]
#[derive(Clone, Copy)]
pub struct TopologyNode {
    pub id: Str,
    pub parent: Str,
    pub label: Str,
    pub kind: i32,
    pub scope: Scope,
    pub health: i32,
    pub origin: i32,
    pub load: f64,
    pub activity: f64,
    pub evidence: Str,
}

/// Header section 8. One communication relationship.
#[repr(C)]
#[derive(Clone, Copy)]
pub struct TopologyLink {
    pub id: Str,
    pub from: Str,
    pub to: Str,
    pub pattern: i32,
    pub origin: i32,
    pub protocol: Str,
    pub health: i32,
    pub volume: u64,
    pub rate: f64,
    pub latency_ms: f64,
    pub progress: f64,
    pub attempts: u32,
    pub evidence: Str,
}

/// Read a publication's text into a handle, or refuse it with a report.
pub type ReadFn = unsafe extern "C" fn(
    text: Str,
    out: *mut *mut Publication,
    report: *mut u8,
    cap: usize,
    out_len: *mut usize,
) -> i32;

/// Release a handle; everything borrowed from it goes with it.
pub type FreeFn = unsafe extern "C" fn(publication: *mut Publication);

/// The head of a read publication.
pub type HeadFn =
    unsafe extern "C" fn(publication: *const Publication, out: *mut PublicationHead) -> i32;

/// The records, in the fill shape.
pub type RecordsFn = unsafe extern "C" fn(
    publication: *const Publication,
    out: *mut HealthEntry,
    cap: usize,
    out_len: *mut usize,
) -> i32;

/// The counts, in the fill shape.
pub type CountsFn = unsafe extern "C" fn(
    publication: *const Publication,
    out: *mut Measurement,
    cap: usize,
    out_len: *mut usize,
) -> i32;

/// The topology's nodes, in the fill shape.
pub type NodesFn = unsafe extern "C" fn(
    publication: *const Publication,
    out: *mut TopologyNode,
    cap: usize,
    out_len: *mut usize,
) -> i32;

/// The topology's links, in the fill shape.
pub type LinksFn = unsafe extern "C" fn(
    publication: *const Publication,
    out: *mut TopologyLink,
    cap: usize,
    out_len: *mut usize,
) -> i32;

/// One of the run's lists, in the fill shape.
pub type RunFn = unsafe extern "C" fn(
    publication: *const Publication,
    list: u32,
    out: *mut Str,
    cap: usize,
    out_len: *mut usize,
) -> i32;

/// Read a curve's text into a handle, or refuse it with a report.
pub type CurveReadFn = unsafe extern "C" fn(
    text: Str,
    out: *mut *mut Curve,
    report: *mut u8,
    cap: usize,
    out_len: *mut usize,
) -> i32;

/// A curve's points, each a measurement at the curve's node, in the fill
/// shape.
pub type CurvePointsFn = unsafe extern "C" fn(
    curve: *const Curve,
    out: *mut Measurement,
    cap: usize,
    out_len: *mut usize,
) -> i32;

/// Release a curve's handle.
pub type CurveFreeFn = unsafe extern "C" fn(curve: *mut Curve);

/// What a topology value is called: the word a publication writes and the
/// name a person reads, both static.
pub type TopologyWordsFn =
    unsafe extern "C" fn(value: i32, out_word: *mut Str, out_name: *mut Str) -> i32;

#[cfg(test)]
mod tests {
    use super::*;

    const HEADER: &str = include_str!("../../include/xmip_operate.h");

    fn defined(name: &str) -> i64 {
        HEADER
            .lines()
            .map(str::trim)
            .find_map(|line| {
                let rest = line.strip_prefix(name)?.trim_start();
                let value = rest.strip_prefix('=')?.trim().trim_end_matches(',');
                value.parse().ok()
            })
            .unwrap_or_else(|| panic!("{name} is not in xmip_operate.h"))
    }

    #[test]
    fn every_entrypoint_is_the_headers() {
        for (define, name) in [
            ("XMIP_PUBLICATION_READ_ENTRYPOINT", READ_ENTRYPOINT),
            ("XMIP_PUBLICATION_FREE_ENTRYPOINT", FREE_ENTRYPOINT),
            ("XMIP_PUBLICATION_HEAD_ENTRYPOINT", HEAD_ENTRYPOINT),
            ("XMIP_PUBLICATION_RECORDS_ENTRYPOINT", RECORDS_ENTRYPOINT),
            ("XMIP_PUBLICATION_COUNTS_ENTRYPOINT", COUNTS_ENTRYPOINT),
            ("XMIP_PUBLICATION_NODES_ENTRYPOINT", NODES_ENTRYPOINT),
            ("XMIP_PUBLICATION_LINKS_ENTRYPOINT", LINKS_ENTRYPOINT),
            ("XMIP_PUBLICATION_RUN_ENTRYPOINT", RUN_ENTRYPOINT),
            ("XMIP_TOPOLOGY_KIND_WORDS_ENTRYPOINT", KIND_WORDS_ENTRYPOINT),
            (
                "XMIP_TOPOLOGY_ORIGIN_WORDS_ENTRYPOINT",
                ORIGIN_WORDS_ENTRYPOINT,
            ),
            (
                "XMIP_TOPOLOGY_PATTERN_WORDS_ENTRYPOINT",
                PATTERN_WORDS_ENTRYPOINT,
            ),
            ("XMIP_CURVE_READ_ENTRYPOINT", CURVE_READ_ENTRYPOINT),
            ("XMIP_CURVE_POINTS_ENTRYPOINT", CURVE_POINTS_ENTRYPOINT),
            ("XMIP_CURVE_FREE_ENTRYPOINT", CURVE_FREE_ENTRYPOINT),
        ] {
            let line = HEADER
                .lines()
                .find(|line| line.starts_with(&format!("#define {define} ")))
                .unwrap_or_else(|| panic!("{define} is not in xmip_operate.h"));

            assert!(line.ends_with(&format!("\"{name}\"")), "{line}");
        }
    }

    #[test]
    fn every_value_is_the_headers() {
        for (name, value) in [
            ("XMIP_TOPOLOGY_COMPUTER", kind::COMPUTER),
            ("XMIP_TOPOLOGY_VIRTUAL_MACHINE", kind::VIRTUAL_MACHINE),
            ("XMIP_TOPOLOGY_LOCATION", kind::LOCATION),
            ("XMIP_TOPOLOGY_CLUSTER", kind::CLUSTER),
            ("XMIP_TOPOLOGY_NODE", kind::NODE),
            ("XMIP_TOPOLOGY_STAGE", kind::STAGE),
            ("XMIP_TOPOLOGY_ENDPOINT", kind::ENDPOINT),
            ("XMIP_ORIGIN_CONFIGURED", origin::CONFIGURED),
            ("XMIP_ORIGIN_BOTH", origin::BOTH),
            ("XMIP_PATTERN_REQUEST_RESPONSE", pattern::REQUEST_RESPONSE),
            ("XMIP_PATTERN_SEND_RECEIVE", pattern::SEND_RECEIVE),
            ("XMIP_PATTERN_RETRY", pattern::RETRY),
        ] {
            assert_eq!(i64::from(value), defined(name), "{name}");
        }
        for (name, value) in [
            ("XMIP_RUN_TESTS", run_list::TESTS),
            ("XMIP_RUN_NODES", run_list::NODES),
            ("XMIP_RUN_CAPABILITIES", run_list::CAPABILITIES),
            ("XMIP_RUN_ONLINE", run_list::ONLINE),
        ] {
            assert_eq!(i64::from(value), defined(name), "{name}");
        }
    }

    #[test]
    fn the_structs_are_plain_c_layouts() {
        let s = size_of::<Str>();
        // Five strings, three ints and two doubles: the ints pad to 16.
        assert_eq!(size_of::<TopologyNode>(), 5 * s + 16 + 16);
        // Five strings, three ints, volume, three doubles, attempts padded.
        assert_eq!(size_of::<TopologyLink>(), 5 * s + 16 + 8 + 24 + 8);
        // Five strings, two flags padded, and when the topology was drawn.
        assert_eq!(size_of::<PublicationHead>(), 5 * s + 8 + 8 + 8);
    }
}
