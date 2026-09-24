//! The operator boundary: `#[repr(C)]` mirrors of `include/xmip_operate.h`.
//!
//! ADR-0027. The module header is the boundary things plug *into*; this is the
//! one that drives Xmip *from outside*, and its shape is opposite: a surface
//! calls functions the runtime implements. It shares [`ffi::Str`] and the
//! status codes with the module boundary and nothing above them, and it
//! versions apart — [`XMIP_OPERATE_VERSION`] is not [`crate::XMIP_ABI_VERSION`].
//!
//! Same rules as `ffi.rs`: the header is normative, the names follow it less
//! the `Xmip` prefix, and where the two disagree the header is right. The
//! tests at the foot read the header itself and check every constant, so that
//! disagreement fails a build rather than surfacing in a surface.
//!
//! Declarations only, no dereferencing, no `unsafe` — the crate's
//! `forbid(unsafe_code)` stands.

use crate::ffi::Str;

/// Header section 1. Versioned apart from the module boundary on purpose.
pub const XMIP_OPERATE_VERSION: u32 = 1;

/// Header section 1. The one symbol a runtime exports for surfaces.
pub const XMIP_OPERATE_ENTRYPOINT: &str = "xmip_operate_v1";

/// Header section 1. Optional wake-up signal for observer surfaces.
pub const XMIP_WAIT_CHANGE_ENTRYPOINT: &str = "xmip_wait_change_v1";

/// Header section 2. An Xmip URI, borrowed. The one scope tree, ADR-0027
/// clause 4, with a Party as a query filter and never a level.
pub type Scope = Str;

/// Header section 3, as it crosses: an `int`. Health is a mood, not a colour
/// (observability-model §6). The leaf moods in worsening order, then `HOLDING`,
/// the rollup mood a parent shows when anything below it is not `FINE` (ADR-0041).
pub mod health {
    pub const FINE: i32 = 0;
    pub const PAUSED: i32 = 1;
    pub const WORKING: i32 = 2;
    pub const STRESSED: i32 = 3;
    pub const EXHAUSTED: i32 = 4;
    pub const DONE: i32 = 5;
    pub const HOLDING: i32 = 6;
}

/// Header section 4, as it crosses.
pub mod counted {
    pub const STREAMS: i32 = 1;
    pub const MESSAGES: i32 = 2;
    pub const JOURNEYS: i32 = 3;
    pub const BYTES: i32 = 4;
    pub const RETRYING: i32 = 5;
    pub const FAILED: i32 = 6;
}

// The typed `Health` and `Counted` enums live in `xmip-core-observe`, which owns
// the domain model (observability-model.md section 6, ADR-0027 clause 5). This
// crate keeps only the wire vocabulary — the `health::` and `counted::` int
// constants and the `#[repr(C)]` structs below — and the one conversion between
// the enum and the wire int lives at the runtime bridge (`runtime/src/wire.rs`,
// split out of `operate.rs` on 2026-09-14), the single place that has both.
// ADR-0009-era duplication removed 2026-09-06.
//
// That placement was re-examined on 2026-09-19 and kept (ADR-0058). Bringing
// the table here would make a Foundation crate depend on `xmip-core-observe`,
// which is Operation; taking the constants to `observe` would put the wire
// vocabulary somewhere the header cannot be checked against it. The bridge is
// the only crate that legitimately has both.

/// Header section 3. One scope's health and the evidence behind it.
#[repr(C)]
#[derive(Clone, Copy)]
pub struct HealthEntry {
    pub scope: Scope,
    pub health: i32,
    /// How far from healthy, 0 to 100, shading the colour. Paused is 30.
    pub severity: u8,
    pub evidence: Str,
    pub observed_unix_nanos: i64,
}

/// Header section 4. A scope, what was counted, the value, its window, and
/// when it was taken — the last so that staleness is visible.
#[repr(C)]
#[derive(Clone, Copy)]
pub struct Measurement {
    pub scope: Scope,
    pub counted: i32,
    pub value: u64,
    pub window_start_unix_nanos: i64,
    pub window_end_unix_nanos: i64,
    pub observed_unix_nanos: i64,
}

/// `health` in the table. Fill up to `cap`, report the true count in
/// `out_len`, return a status.
pub type HealthFn = unsafe extern "C" fn(
    ctx: *mut u8,
    scope: Scope,
    out: *mut HealthEntry,
    cap: usize,
    out_len: *mut usize,
) -> i32;

/// `measure` in the table. Same shape.
pub type MeasureFn = unsafe extern "C" fn(
    ctx: *mut u8,
    scope: Scope,
    counted: i32,
    out: *mut Measurement,
    cap: usize,
    out_len: *mut usize,
) -> i32;

/// `pause` in the table: everything at and beneath the scope, by `who`.
pub type PauseFn = unsafe extern "C" fn(ctx: *mut u8, scope: Scope, who: Str) -> i32;

/// `resume` in the table.
pub type ResumeFn = unsafe extern "C" fn(ctx: *mut u8, scope: Scope) -> i32;

/// `destroy` in the table.
pub type DestroyFn = unsafe extern "C" fn(ctx: *mut u8);

/// Header section 5. What a surface calls. Filled by the runtime, held by the
/// surface. No "count now", no "refresh": a surface reads what was published.
#[repr(C)]
pub struct Operate {
    pub abi_version: u32,
    pub ctx: *mut u8,
    pub health: Option<HealthFn>,
    pub measure: Option<MeasureFn>,
    pub pause: Option<PauseFn>,
    pub resume: Option<ResumeFn>,
    pub destroy: Option<DestroyFn>,
}

/// Header section 5. Wait for the immutable published snapshot to advance.
pub type WaitChangeFn =
    unsafe extern "C" fn(after_revision: u64, timeout_ms: u32, out_revision: *mut u64) -> i32;

/// Header section 5. The exported entrypoint's shape.
pub type OperateFn = unsafe extern "C" fn(version: u32, out: *mut Operate) -> i32;

/// Header section 6. Start a node from a saved configuration file.
pub const XMIP_START_ENTRYPOINT: &str = "xmip_start_v1";

/// Header section 6. Validate configuration text without applying it.
pub const XMIP_VALIDATE_ENTRYPOINT: &str = "xmip_validate_v1";

/// Header section 6. `xmip_start_v1`: a path to a saved configuration.
pub type StartFn = unsafe extern "C" fn(path: Str) -> i32;

/// Header section 6. `xmip_validate_v1`: configuration text in, the report
/// written into `report` as UTF-8, its true length in `out_len`.
pub type ValidateFn = unsafe extern "C" fn(
    configuration: Str,
    report: *mut u8,
    cap: usize,
    out_len: *mut usize,
) -> i32;

/// Header section 7: the rules a surface calls instead of keeping its own.
/// Each is a separate optional symbol the runtime exports, a thin forwarder
/// into the crate that owns the rule; none reads the snapshot.
pub mod rule {
    use super::{HealthEntry, Scope};
    use crate::ffi::Str;

    /// `observe::Scope::contains`.
    pub const SCOPE_CONTAINS_ENTRYPOINT: &str = "xmip_scope_contains_v1";
    /// `observe::Scope::segments`.
    pub const SCOPE_PARTS_ENTRYPOINT: &str = "xmip_scope_parts_v1";
    /// `node::Stage::WORDS`.
    pub const STAGE_WORDS_ENTRYPOINT: &str = "xmip_stage_words_v1";
    /// `node::Stage::declared`.
    pub const STAGE_DECLARED_ENTRYPOINT: &str = "xmip_stage_declared_v1";
    /// `observe::Health::word`.
    pub const HEALTH_WORD_ENTRYPOINT: &str = "xmip_health_word_v1";
    /// `observe::Health::color`.
    pub const HEALTH_COLOR_ENTRYPOINT: &str = "xmip_health_color_v1";
    /// `observe::Health::named`.
    pub const HEALTH_NAMED_ENTRYPOINT: &str = "xmip_health_named_v1";
    /// `observe::Standing::worst_first`.
    pub const HEALTH_ORDER_ENTRYPOINT: &str = "xmip_health_order_v1";
    /// `observe::Health::rolled`.
    pub const HEALTH_ROLLED_ENTRYPOINT: &str = "xmip_health_rolled_v1";
    /// `observe::Counted::word`.
    pub const COUNTED_WORD_ENTRYPOINT: &str = "xmip_counted_word_v1";
    /// `observe::Counted::at`.
    pub const STAGE_COUNTED_ENTRYPOINT: &str = "xmip_stage_counted_v1";
    /// `node::Stage::pausable`.
    pub const STAGE_PAUSABLE_ENTRYPOINT: &str = "xmip_stage_pausable_v1";
    /// `node::Stage::location`.
    pub const STAGE_LOCATION_ENTRYPOINT: &str = "xmip_stage_location_v1";
    /// `observe::capability::declared`.
    pub const CAPABILITY_PUBLISHED_ENTRYPOINT: &str = "xmip_capability_published_v1";
    /// `node::Capability::from_entry`.
    pub const CAPABILITY_ENTRY_ENTRYPOINT: &str = "xmip_capability_entry_v1";

    /// Whether `candidate` is `scope` or beneath it; `out_contains` 1 or 0.
    pub type ScopeContainsFn =
        unsafe extern "C" fn(scope: Scope, candidate: Scope, out_contains: *mut u8) -> i32;

    /// A scope's segments, borrowed from `scope`, in the fill shape.
    pub type ScopePartsFn =
        unsafe extern "C" fn(scope: Scope, out: *mut Str, cap: usize, out_len: *mut usize) -> i32;

    /// The stage words, static, in the fill shape.
    pub type StageWordsFn =
        unsafe extern "C" fn(out: *mut Str, cap: usize, out_len: *mut usize) -> i32;

    /// A declaration's stages, or its refusal written as UTF-8.
    pub type StageDeclaredFn = unsafe extern "C" fn(
        declared: Str,
        stages: *mut Str,
        cap: usize,
        out_len: *mut usize,
        refusal: *mut u8,
        refusal_cap: usize,
        refusal_len: *mut usize,
    ) -> i32;

    /// A mood's word or color name, static.
    pub type HealthTextFn = unsafe extern "C" fn(health: i32, out: *mut Str) -> i32;

    /// The mood a word names.
    pub type HealthNamedFn = unsafe extern "C" fn(word: Str, out: *mut i32) -> i32;

    /// Many entries worst first, as their positions.
    pub type HealthOrderFn =
        unsafe extern "C" fn(entries: *const HealthEntry, len: usize, out_order: *mut usize) -> i32;

    /// What a parent shows over a mood: Fine or Holding.
    pub type HealthRolledFn = unsafe extern "C" fn(health: i32, out: *mut i32) -> i32;

    /// A counted kind's word, static.
    pub type CountedWordFn = unsafe extern "C" fn(counted: i32, out: *mut Str) -> i32;

    /// The kind a stage counts.
    pub type StageCountedFn = unsafe extern "C" fn(stage: Str, out: *mut i32) -> i32;

    /// Whether a stage may be paused; `out` 1 or 0.
    pub type StagePausableFn = unsafe extern "C" fn(stage: Str, out: *mut u8) -> i32;

    /// What a thing at a stage is called, static.
    pub type StageLocationFn = unsafe extern "C" fn(stage: Str, out: *mut Str) -> i32;

    /// The capability a node published: its name (borrowed from `scope`),
    /// its stages, its online capability, or its refusal.
    pub type CapabilityPublishedFn = unsafe extern "C" fn(
        scope: Scope,
        evidence: Str,
        out_node: *mut Str,
        stages: *mut Str,
        cap: usize,
        out_len: *mut usize,
        out_online: *mut u8,
        refusal: *mut u8,
        refusal_cap: usize,
        refusal_len: *mut usize,
    ) -> i32;

    /// A run's entry for a node: its name (borrowed from `entry`), its
    /// stages, or its refusal.
    pub type CapabilityEntryFn = unsafe extern "C" fn(
        entry: Str,
        out_node: *mut Str,
        stages: *mut Str,
        cap: usize,
        out_len: *mut usize,
        refusal: *mut u8,
        refusal_cap: usize,
        refusal_len: *mut usize,
    ) -> i32;
}

pub mod publication;

#[cfg(test)]
mod tests {
    use super::*;

    /// The header itself, at compile time. Moving or renaming it fails here.
    const HEADER: &str = include_str!("../include/xmip_operate.h");

    /// `NAME = value,` or `#define NAME value` in the header, as an integer.
    fn header_value(name: &str) -> i64 {
        for line in HEADER.lines() {
            let line = line.trim();

            if let Some(rest) = line.strip_prefix("#define ") {
                let mut parts = rest.split_whitespace();

                if parts.next() == Some(name) {
                    let raw = parts.next().expect("a value").trim_end_matches('u');

                    return raw.parse().expect("an integer");
                }
            }

            if let Some(rest) = line.strip_prefix(name) {
                let rest = rest.trim_start();

                if let Some(value) = rest.strip_prefix('=') {
                    let raw = value.trim().trim_end_matches(',');

                    return raw.parse().expect("an integer");
                }
            }
        }

        panic!("{name} is not in xmip_operate.h");
    }

    #[test]
    fn the_version_matches_the_header_and_is_not_the_module_version() {
        assert_eq!(
            i64::from(XMIP_OPERATE_VERSION),
            header_value("XMIP_OPERATE_VERSION")
        );
        // Versioned apart is the whole point. If these ever coincide by
        // accident, the next bump to one will look like a bump to both.
        assert!(HEADER.contains("versions apart"));
    }

    #[test]
    fn the_entrypoint_matches_the_header() {
        assert!(HEADER.contains(&format!("\"{XMIP_OPERATE_ENTRYPOINT}\"")));
        assert!(HEADER.contains(&format!("\"{XMIP_WAIT_CHANGE_ENTRYPOINT}\"")));
        assert!(HEADER.contains("XmipWaitChangeFn"));
    }

    #[test]
    fn every_rule_entrypoint_matches_the_header() {
        for (define, name) in [
            (
                "XMIP_SCOPE_CONTAINS_ENTRYPOINT",
                rule::SCOPE_CONTAINS_ENTRYPOINT,
            ),
            ("XMIP_SCOPE_PARTS_ENTRYPOINT", rule::SCOPE_PARTS_ENTRYPOINT),
            ("XMIP_STAGE_WORDS_ENTRYPOINT", rule::STAGE_WORDS_ENTRYPOINT),
            (
                "XMIP_STAGE_DECLARED_ENTRYPOINT",
                rule::STAGE_DECLARED_ENTRYPOINT,
            ),
            ("XMIP_HEALTH_WORD_ENTRYPOINT", rule::HEALTH_WORD_ENTRYPOINT),
            (
                "XMIP_HEALTH_COLOR_ENTRYPOINT",
                rule::HEALTH_COLOR_ENTRYPOINT,
            ),
            (
                "XMIP_HEALTH_NAMED_ENTRYPOINT",
                rule::HEALTH_NAMED_ENTRYPOINT,
            ),
            (
                "XMIP_HEALTH_ORDER_ENTRYPOINT",
                rule::HEALTH_ORDER_ENTRYPOINT,
            ),
            (
                "XMIP_HEALTH_ROLLED_ENTRYPOINT",
                rule::HEALTH_ROLLED_ENTRYPOINT,
            ),
            (
                "XMIP_COUNTED_WORD_ENTRYPOINT",
                rule::COUNTED_WORD_ENTRYPOINT,
            ),
            (
                "XMIP_STAGE_COUNTED_ENTRYPOINT",
                rule::STAGE_COUNTED_ENTRYPOINT,
            ),
            (
                "XMIP_STAGE_PAUSABLE_ENTRYPOINT",
                rule::STAGE_PAUSABLE_ENTRYPOINT,
            ),
            (
                "XMIP_STAGE_LOCATION_ENTRYPOINT",
                rule::STAGE_LOCATION_ENTRYPOINT,
            ),
            (
                "XMIP_CAPABILITY_PUBLISHED_ENTRYPOINT",
                rule::CAPABILITY_PUBLISHED_ENTRYPOINT,
            ),
            (
                "XMIP_CAPABILITY_ENTRY_ENTRYPOINT",
                rule::CAPABILITY_ENTRY_ENTRYPOINT,
            ),
            ("XMIP_START_ENTRYPOINT", XMIP_START_ENTRYPOINT),
            ("XMIP_VALIDATE_ENTRYPOINT", XMIP_VALIDATE_ENTRYPOINT),
        ] {
            let line = HEADER
                .lines()
                .find(|line| line.starts_with(&format!("#define {define} ")))
                .unwrap_or_else(|| panic!("{define} is not in xmip_operate.h"));

            assert!(line.ends_with(&format!("\"{name}\"")), "{line}");
        }
    }

    #[test]
    fn every_health_value_matches_the_header() {
        assert_eq!(i64::from(health::FINE), header_value("XMIP_HEALTH_FINE"));
        assert_eq!(
            i64::from(health::PAUSED),
            header_value("XMIP_HEALTH_PAUSED")
        );
        assert_eq!(
            i64::from(health::WORKING),
            header_value("XMIP_HEALTH_WORKING")
        );
        assert_eq!(
            i64::from(health::STRESSED),
            header_value("XMIP_HEALTH_STRESSED")
        );
        assert_eq!(
            i64::from(health::EXHAUSTED),
            header_value("XMIP_HEALTH_EXHAUSTED")
        );
        assert_eq!(i64::from(health::DONE), header_value("XMIP_HEALTH_DONE"));
        assert_eq!(
            i64::from(health::HOLDING),
            header_value("XMIP_HEALTH_HOLDING")
        );
    }

    #[test]
    fn every_counted_value_matches_the_header() {
        assert_eq!(
            i64::from(counted::STREAMS),
            header_value("XMIP_COUNTED_STREAMS")
        );
        assert_eq!(
            i64::from(counted::MESSAGES),
            header_value("XMIP_COUNTED_MESSAGES")
        );
        assert_eq!(
            i64::from(counted::JOURNEYS),
            header_value("XMIP_COUNTED_JOURNEYS")
        );
        assert_eq!(
            i64::from(counted::BYTES),
            header_value("XMIP_COUNTED_BYTES")
        );
        assert_eq!(
            i64::from(counted::RETRYING),
            header_value("XMIP_COUNTED_RETRYING")
        );
        assert_eq!(
            i64::from(counted::FAILED),
            header_value("XMIP_COUNTED_FAILED")
        );
    }

    #[test]
    fn the_wire_structs_are_plain_c_layouts() {
        // repr(C) with only scalars and Str inside, so a surface in any
        // language reads them by offset. Sizes are what C would compute.
        // i32 health + u8 severity + 3 bytes of padding is the second 8.
        assert_eq!(size_of::<HealthEntry>(), 2 * size_of::<Str>() + 8 + 8);
        assert_eq!(size_of::<Measurement>(), size_of::<Str>() + 8 + 8 + 3 * 8);
    }

    #[test]
    fn the_header_includes_the_module_header_and_defines_no_primitive_twice() {
        // Sections 2, 3 and 5 are shared and nothing above them is. A second
        // XmipStr would be two definitions of one thing across two audiences.
        assert!(HEADER.contains("#include \"xmip_module.h\""));
        assert!(!HEADER.contains("} XmipStr;"));
        assert!(!HEADER.contains("} XmipSlice;"));
        // The definition, not a mention: the operator header may name a status
        // in prose (it does, describing what validate returns) but must not
        // #define one — those belong to xmip_module.h it includes.
        assert!(!HEADER.contains("#define XMIP_OK"));
    }
}
