//! Header section 25: every Report in one list, each saying whether it
//! presents data that already exists or what a Quest probes first, and one
//! opened (ADR-0072, amendment 2026-11-10). The list and each entry's kind
//! are `xmip-core-report`'s; the runtime's library forwards to it.
//! Declarations only; the runtime's tests fail to compile if the export
//! drifts from [`ReportFn`].

use crate::ffi::Str;

/// `xmip_report_v1`.
pub const REPORT_ENTRYPOINT: &str = "xmip_report_v1";

/// Header section 25, `XmipReportKind`, as it crosses: what a Report
/// presents.
pub mod kind {
    /// It presents data that already exists: quick.
    pub const EXISTING_DATA: i32 = 0;
    /// It presents what a Quest probes first: it takes longer.
    pub const QUEST: i32 = 1;
}

/// `xmip_report_v1`: the act `act` (`list` or `open`) on `arguments`, a
/// JSON object, answered as JSON in `out` as UTF-8, its true length in
/// `out_len` whether or not it fit.
pub type ReportFn = unsafe extern "C" fn(
    act: Str,
    arguments: Str,
    out: *mut u8,
    cap: usize,
    out_len: *mut usize,
) -> i32;
