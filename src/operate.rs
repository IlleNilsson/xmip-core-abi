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

/// Header section 2. An Xmip URI, borrowed. The one scope tree, ADR-0027
/// clause 4, with a Party as a query filter and never a level.
pub type Scope = Str;

/// Header section 3, as it crosses: an `int`.
pub mod health {
    pub const GREEN: i32 = 0;
    pub const YELLOW: i32 = 1;
    pub const RED: i32 = 2;

    /// A surface's word, never a node's. A node that answers is reachable;
    /// this is what an aggregating surface says about one that did not.
    /// ADR-0027 clause 8.
    pub const UNREACHABLE: i32 = 3;
}

/// Header section 4, as it crosses.
pub mod counted {
    pub const STREAMS: i32 = 1;
    pub const MESSAGES: i32 = 2;
    pub const JOURNEYS: i32 = 3;
    pub const BYTES: i32 = 4;
}

/// Health, typed. observability-model.md section 6: worst active state wins
/// upward, and that ordering is this enum's ordering.
#[derive(Clone, Copy, Debug, PartialEq, Eq, PartialOrd, Ord)]
pub enum Health {
    Green,
    Yellow,
    Red,
    Unreachable,
}

impl Health {
    /// From the wire, or `None` for a value this build does not know.
    #[must_use]
    pub const fn from_wire(value: i32) -> Option<Self> {
        match value {
            health::GREEN => Some(Self::Green),
            health::YELLOW => Some(Self::Yellow),
            health::RED => Some(Self::Red),
            health::UNREACHABLE => Some(Self::Unreachable),
            _ => None,
        }
    }

    /// To the wire.
    #[must_use]
    pub const fn to_wire(self) -> i32 {
        match self {
            Self::Green => health::GREEN,
            Self::Yellow => health::YELLOW,
            Self::Red => health::RED,
            Self::Unreachable => health::UNREACHABLE,
        }
    }

    /// The worse of two, which is how health propagates up the tree.
    #[must_use]
    pub fn worst(self, other: Self) -> Self {
        if other > self { other } else { self }
    }
}

/// What a measurement counts, typed. Never a bare number — ADR-0027 clause 5.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum Counted {
    Streams,
    Messages,
    Journeys,
    Bytes,
}

impl Counted {
    /// From the wire, or `None` for a value this build does not know.
    #[must_use]
    pub const fn from_wire(value: i32) -> Option<Self> {
        match value {
            counted::STREAMS => Some(Self::Streams),
            counted::MESSAGES => Some(Self::Messages),
            counted::JOURNEYS => Some(Self::Journeys),
            counted::BYTES => Some(Self::Bytes),
            _ => None,
        }
    }

    /// To the wire.
    #[must_use]
    pub const fn to_wire(self) -> i32 {
        match self {
            Self::Streams => counted::STREAMS,
            Self::Messages => counted::MESSAGES,
            Self::Journeys => counted::JOURNEYS,
            Self::Bytes => counted::BYTES,
        }
    }

    /// Whether the value is a byte count rather than a count of things.
    #[must_use]
    pub const fn is_bytes(self) -> bool {
        matches!(self, Self::Bytes)
    }
}

/// Header section 3. One scope's health and the evidence behind it.
#[repr(C)]
#[derive(Clone, Copy)]
pub struct HealthEntry {
    pub scope: Scope,
    pub health: i32,
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
    pub destroy: Option<DestroyFn>,
}

/// Header section 5. The exported entrypoint's shape.
pub type OperateFn = unsafe extern "C" fn(version: u32, out: *mut Operate) -> i32;

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
    }

    #[test]
    fn every_health_value_matches_the_header() {
        assert_eq!(i64::from(health::GREEN), header_value("XMIP_HEALTH_GREEN"));
        assert_eq!(
            i64::from(health::YELLOW),
            header_value("XMIP_HEALTH_YELLOW")
        );
        assert_eq!(i64::from(health::RED), header_value("XMIP_HEALTH_RED"));
        assert_eq!(
            i64::from(health::UNREACHABLE),
            header_value("XMIP_HEALTH_UNREACHABLE")
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
    }

    #[test]
    fn health_round_trips_and_refuses_the_unknown() {
        for value in [
            Health::Green,
            Health::Yellow,
            Health::Red,
            Health::Unreachable,
        ] {
            assert_eq!(Health::from_wire(value.to_wire()), Some(value));
        }

        assert_eq!(Health::from_wire(99), None);
    }

    #[test]
    fn worst_state_wins_upward() {
        // observability-model.md section 6: an installation showing green
        // means every endpoint beneath it is green.
        assert_eq!(Health::Green.worst(Health::Yellow), Health::Yellow);
        assert_eq!(Health::Red.worst(Health::Yellow), Health::Red);
        assert_eq!(Health::Unreachable.worst(Health::Red), Health::Unreachable);
        assert_eq!(Health::Green.worst(Health::Green), Health::Green);
    }

    #[test]
    fn counted_round_trips_and_only_bytes_is_bytes() {
        for value in [
            Counted::Streams,
            Counted::Messages,
            Counted::Journeys,
            Counted::Bytes,
        ] {
            assert_eq!(Counted::from_wire(value.to_wire()), Some(value));
        }

        assert!(Counted::Bytes.is_bytes());
        assert!(!Counted::Journeys.is_bytes());
        assert_eq!(Counted::from_wire(0), None);
    }

    #[test]
    fn the_wire_structs_are_plain_c_layouts() {
        // repr(C) with only scalars and Str inside, so a surface in any
        // language reads them by offset. Sizes are what C would compute.
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
        assert!(!HEADER.contains("XMIP_OK "));
    }
}
