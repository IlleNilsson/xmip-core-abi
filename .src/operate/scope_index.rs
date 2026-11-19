//! Header section 28.1: a publication's scope tree, built once and asked by
//! scope, every record answered by its place in what the surface read
//! (ADR-0052, amendment 2026-11-18); and the scope tree's composed rules a
//! surface asks rather than keeps: a scope's parent, its node's own scope
//! and its name, and six figures summed. Declarations only; the runtime's
//! tests fail to compile if an export drifts from its type here.

use crate::ffi::Str;
use crate::operate::publication::Publication;
use crate::operate::{HealthEntry, Measurement};

/// A publication's scope tree, opaque: only the runtime knows what is behind
/// it. Built by section 28.1's exports or read with a surface's publication
/// (section 28.2), and freed by `xmip_scope_index_free_v1`; immutable while
/// it lives, so what is borrowed from it stays valid until it is freed.
#[repr(C)]
pub struct ScopeIndex {
    _private: [u8; 0],
}

/// One scope as the tree holds it. Every string and the places are borrowed
/// from the index; `own` and the places index the records the index was
/// built from, in their order.
#[repr(C)]
#[derive(Clone, Copy)]
pub struct ScopePlaced {
    /// The scope as the tree spells it.
    pub scope: Str,
    /// The segment it is called by.
    pub label: Str,
    /// The records at and beneath it, worst first, by place.
    pub leaves: *const u32,
    pub leaves_len: usize,
    /// The record published at it itself, the last there, or -1.
    pub own: i64,
    /// Its mood, a section 3 health: its worst record's as a leaf, the
    /// rollup as a parent; -1 where nothing is recorded.
    pub mood: i32,
    /// Its mood as its row says it: the rollup where records lie beneath
    /// it, else its worst record's; -1 where nothing is recorded.
    pub row_mood: i32,
    pub is_leaf: u8,
    pub is_container: u8,
    pub begins_stage: u8,
    /// Bit k is set where figure k was published, k in [`figure`] order.
    pub published: u8,
    /// The stage it is on, its word; empty on none.
    pub stage: Str,
    pub streams: u64,
    pub messages: u64,
    pub journeys: u64,
    pub bytes: u64,
    pub retrying: u64,
    pub failed: u64,
}

/// What the index is: its revision, how many records and scopes, when its
/// newest figure was observed, and who published it, borrowed.
#[repr(C)]
#[derive(Clone, Copy)]
pub struct ScopeIndexFacts {
    pub revision: u64,
    pub leaves: usize,
    pub scopes: usize,
    pub observed_unix_nanos: i64,
    /// 1 where `observed_unix_nanos` says anything.
    pub observed: u8,
    pub source: Str,
}

/// Six figures and when the newest was observed: what
/// `xmip_figures_sum_v1` adds up and answers.
#[repr(C)]
#[derive(Clone, Copy, Default)]
pub struct Figures {
    pub streams: u64,
    pub messages: u64,
    pub journeys: u64,
    pub bytes: u64,
    pub retrying: u64,
    pub failed: u64,
    pub observed_unix_nanos: i64,
    /// Bit k is set where figure k was published, k in [`figure`] order.
    pub published: u8,
    /// 1 where `observed_unix_nanos` says anything.
    pub observed: u8,
}

/// The bit of each figure in a `published` mask.
pub mod figure {
    pub const STREAMS: u8 = 1;
    pub const MESSAGES: u8 = 1 << 1;
    pub const JOURNEYS: u8 = 1 << 2;
    pub const BYTES: u8 = 1 << 3;
    pub const RETRYING: u8 = 1 << 4;
    pub const FAILED: u8 = 1 << 5;
}

/// Which list `xmip_scope_index_list_v1` answers.
pub mod list {
    /// The scopes where the stage the argument names begins.
    pub const STAGE_SCOPES: u32 = 1;
    /// The names directly beneath the root; no argument.
    pub const NODES: u32 = 2;
    /// The scopes the pattern the argument is matches.
    pub const MATCHING: u32 = 3;
}

/// Which scope `xmip_scope_relative_v1` answers.
pub mod relative {
    /// One step up; the root's is the root.
    pub const PARENT: u32 = 1;
    /// The node's own scope it is on or beneath; empty on no node.
    pub const NODE_SCOPE: u32 = 2;
    /// What it is called beneath its cluster.
    pub const NAME: u32 = 3;
}

pub const SCOPE_INDEX_BUILD_ENTRYPOINT: &str = "xmip_scope_index_build_v1";
pub const SCOPE_INDEX_OF_PUBLICATION_ENTRYPOINT: &str = "xmip_scope_index_of_publication_v1";
pub const SCOPE_INDEX_FREE_ENTRYPOINT: &str = "xmip_scope_index_free_v1";
pub const SCOPE_INDEX_FACTS_ENTRYPOINT: &str = "xmip_scope_index_facts_v1";
pub const SCOPE_INDEX_PLACED_ENTRYPOINT: &str = "xmip_scope_index_placed_v1";
pub const SCOPE_INDEX_CHILDREN_ENTRYPOINT: &str = "xmip_scope_index_children_v1";
pub const SCOPE_INDEX_LIST_ENTRYPOINT: &str = "xmip_scope_index_list_v1";
pub const SCOPE_INDEX_LABEL_ENTRYPOINT: &str = "xmip_scope_index_label_v1";
pub const SCOPE_INDEX_WORST_AT_STAGE_ENTRYPOINT: &str = "xmip_scope_index_worst_at_stage_v1";
pub const SCOPE_INDEX_MEASURE_ENTRYPOINT: &str = "xmip_scope_index_measure_v1";
pub const SCOPE_INDEX_CAPABILITIES_ENTRYPOINT: &str = "xmip_scope_index_capabilities_v1";
pub const SCOPE_INDEX_SELECT_ENTRYPOINT: &str = "xmip_scope_index_select_v1";
pub const SCOPE_RELATIVE_ENTRYPOINT: &str = "xmip_scope_relative_v1";
pub const FIGURES_SUM_ENTRYPOINT: &str = "xmip_figures_sum_v1";

/// An index built from records and counts, each record keeping its place.
pub type ScopeIndexBuildFn = unsafe extern "C" fn(
    records: *const HealthEntry,
    records_len: usize,
    counts: *const Measurement,
    counts_len: usize,
    revision: u64,
    source: Str,
    out: *mut *mut ScopeIndex,
) -> i32;

/// An index built from a read publication's records and counts.
pub type ScopeIndexOfPublicationFn = unsafe extern "C" fn(
    publication: *const Publication,
    revision: u64,
    source: Str,
    out: *mut *mut ScopeIndex,
) -> i32;

/// Release an index.
pub type ScopeIndexFreeFn = unsafe extern "C" fn(index: *mut ScopeIndex);

/// What an index is.
pub type ScopeIndexFactsFn =
    unsafe extern "C" fn(index: *const ScopeIndex, out: *mut ScopeIndexFacts) -> i32;

/// One scope as the index holds it.
pub type ScopeIndexPlacedFn =
    unsafe extern "C" fn(index: *const ScopeIndex, scope: Str, out: *mut ScopePlaced) -> i32;

/// The scopes directly beneath one, worst first, in the fill shape.
pub type ScopeIndexChildrenFn = unsafe extern "C" fn(
    index: *const ScopeIndex,
    scope: Str,
    out: *mut ScopePlaced,
    cap: usize,
    out_len: *mut usize,
) -> i32;

/// A list of scopes or names, borrowed, in the fill shape.
pub type ScopeIndexListFn = unsafe extern "C" fn(
    index: *const ScopeIndex,
    which: u32,
    argument: Str,
    out: *mut Str,
    cap: usize,
    out_len: *mut usize,
) -> i32;

/// What a person reads a scope as.
pub type ScopeIndexLabelFn =
    unsafe extern "C" fn(index: *const ScopeIndex, scope: Str, out: *mut Str) -> i32;

/// The place of the worst record on a stage, or -1.
pub type ScopeIndexWorstAtStageFn =
    unsafe extern "C" fn(index: *const ScopeIndex, stage: Str, out_place: *mut i64) -> i32;

/// One figure at a scope, as a measurement.
pub type ScopeIndexMeasureFn = unsafe extern "C" fn(
    index: *const ScopeIndex,
    scope: Str,
    counted: i32,
    now_unix_nanos: i64,
    out: *mut Measurement,
) -> i32;

/// Every node's declaration, as JSON.
pub type ScopeIndexCapabilitiesFn = unsafe extern "C" fn(
    index: *const ScopeIndex,
    out: *mut u8,
    cap: usize,
    out_len: *mut usize,
) -> i32;

/// What a pattern selects among the index's scopes, as JSON, or the
/// refusal of one that matches none.
pub type ScopeIndexSelectFn = unsafe extern "C" fn(
    index: *const ScopeIndex,
    pattern: Str,
    source: Str,
    out: *mut u8,
    cap: usize,
    out_len: *mut usize,
) -> i32;

/// A scope's parent, its node's own scope or its name.
pub type ScopeRelativeFn = unsafe extern "C" fn(
    scope: Str,
    which: u32,
    out: *mut u8,
    cap: usize,
    out_len: *mut usize,
) -> i32;

/// Figures summed.
pub type FiguresSumFn =
    unsafe extern "C" fn(parts: *const Figures, len: usize, out: *mut Figures) -> i32;

#[cfg(test)]
mod tests {
    use super::*;

    /// The header itself, at compile time.
    const HEADER: &str = include_str!("../../include/xmip_operate.h");

    #[test]
    fn every_scope_index_entrypoint_and_word_is_the_header_s() {
        for name in [
            SCOPE_INDEX_BUILD_ENTRYPOINT,
            SCOPE_INDEX_OF_PUBLICATION_ENTRYPOINT,
            SCOPE_INDEX_FREE_ENTRYPOINT,
            SCOPE_INDEX_FACTS_ENTRYPOINT,
            SCOPE_INDEX_PLACED_ENTRYPOINT,
            SCOPE_INDEX_CHILDREN_ENTRYPOINT,
            SCOPE_INDEX_LIST_ENTRYPOINT,
            SCOPE_INDEX_LABEL_ENTRYPOINT,
            SCOPE_INDEX_WORST_AT_STAGE_ENTRYPOINT,
            SCOPE_INDEX_MEASURE_ENTRYPOINT,
            SCOPE_INDEX_CAPABILITIES_ENTRYPOINT,
            SCOPE_INDEX_SELECT_ENTRYPOINT,
            SCOPE_RELATIVE_ENTRYPOINT,
            FIGURES_SUM_ENTRYPOINT,
        ] {
            assert!(HEADER.contains(&format!("\"{name}\"")), "{name}");
        }

        for (define, value) in [
            ("XMIP_SCOPE_LIST_STAGE_SCOPES", list::STAGE_SCOPES),
            ("XMIP_SCOPE_LIST_NODES", list::NODES),
            ("XMIP_SCOPE_LIST_MATCHING", list::MATCHING),
            ("XMIP_SCOPE_RELATIVE_PARENT", relative::PARENT),
            ("XMIP_SCOPE_RELATIVE_NODE_SCOPE", relative::NODE_SCOPE),
            ("XMIP_SCOPE_RELATIVE_NAME", relative::NAME),
        ] {
            assert!(
                HEADER.contains(&format!("#define {define} {value}u")),
                "{define}"
            );
        }

        for (define, bit) in [
            ("XMIP_FIGURE_STREAMS", figure::STREAMS),
            ("XMIP_FIGURE_MESSAGES", figure::MESSAGES),
            ("XMIP_FIGURE_JOURNEYS", figure::JOURNEYS),
            ("XMIP_FIGURE_BYTES", figure::BYTES),
            ("XMIP_FIGURE_RETRYING", figure::RETRYING),
            ("XMIP_FIGURE_FAILED", figure::FAILED),
        ] {
            assert!(
                HEADER.contains(&format!("#define {define} {bit}u")),
                "{define}"
            );
        }
    }

    #[test]
    fn the_structs_are_laid_out_as_the_header_s() {
        // Two strings, a pointer, a length, i64, two i32, four bytes, a
        // string, six u64: 72 bytes before the stage, 136 in all on a 64-bit
        // target.
        if size_of::<usize>() == 8 {
            assert_eq!(size_of::<ScopePlaced>(), 136);
            assert_eq!(size_of::<ScopeIndexFacts>(), 56);
            assert_eq!(size_of::<Figures>(), 64);
        }
    }
}
