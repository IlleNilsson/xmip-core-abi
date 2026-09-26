//! Header section 12: the technologies a runtime carries, and the settings
//! each declares (ADR-0064, amendment 2026-09-26).
//!
//! A Location's form is never written in a surface: every technology
//! declares its own settings in its own crate, the runtime's library holds
//! the declarations of the technologies it carries, and the language server
//! and the desktop editor read the one answer through this export. A thin
//! forwarder; the declaration's shape is `xmip-core`'s `settings`.
//! Declarations only; the runtime's tests fail to compile if the export
//! drifts from [`CatalogueFn`].

use crate::ffi::Str;

/// `xmip_technology_catalogue_v1`: the technologies the runtime carries,
/// each with its capability and its settings, as JSON in memory only
/// (ADR-0031 clause 2); one technology's alone when it is named.
pub const TECHNOLOGY_CATALOGUE_ENTRYPOINT: &str = "xmip_technology_catalogue_v1";

/// `technology` empty for every technology carried, or a module name for
/// that one alone. The answer is written into `out` as UTF-8, its true byte
/// length in `out_len` whether or not it fit. `XMIP_OK` with the answer;
/// `XMIP_E_INVALID` with the refusal, one sentence, when the runtime carries
/// no technology of that name; `XMIP_E_MALFORMED` when it is not UTF-8.
pub type CatalogueFn =
    unsafe extern "C" fn(technology: Str, out: *mut u8, cap: usize, out_len: *mut usize) -> i32;
