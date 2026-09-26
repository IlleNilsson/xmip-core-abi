//! Header section 10: an Xmip Application, read and edited for a designer
//! (ADR-0064).
//!
//! The routes designer in the VS Code extension holds no rule: its language
//! server asks the runtime's library, which forwards each call to
//! `xmip-core-configure`, where the Application, its filters and its edits
//! are read. Four exports, one shape: text in, the answer written into the
//! caller's buffer as UTF-8 and its true length in `out_len`, the way
//! `xmip_validate_v1` writes its report. Declarations only; the runtime's
//! tests fail to compile if an export drifts from [`DesignFn`].

use crate::ffi::Str;

/// `xmip_application_routes_v1`: an Application's text in, its routes as a
/// graph out (JSON, in memory only — ADR-0031 clause 2).
pub const APPLICATION_ROUTES_ENTRYPOINT: &str = "xmip_application_routes_v1";

/// `xmip_filter_structure_v1`: a filter's text in, its rows and groups out.
pub const FILTER_STRUCTURE_ENTRYPOINT: &str = "xmip_filter_structure_v1";

/// `xmip_filter_text_v1`: rows and groups in, the filter's canonical text out.
pub const FILTER_TEXT_ENTRYPOINT: &str = "xmip_filter_text_v1";

/// `xmip_application_edit_v1`: an Application's text and an edit in, the
/// edited text out.
pub const APPLICATION_EDIT_ENTRYPOINT: &str = "xmip_application_edit_v1";

/// The one shape of the four. `input` is the text the export reads,
/// `argument` the edit where one is taken and empty otherwise. `XMIP_OK`
/// with the answer in `out`; `XMIP_E_INVALID` with the refusal, one
/// sentence, in `out`; `XMIP_E_MALFORMED` when either is not UTF-8.
pub type DesignFn = unsafe extern "C" fn(
    input: Str,
    argument: Str,
    out: *mut u8,
    cap: usize,
    out_len: *mut usize,
) -> i32;
