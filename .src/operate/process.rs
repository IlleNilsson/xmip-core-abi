//! Header section 13: a System Process, declared (ADR-0053 clause 3).
//!
//! The declaration's file, its directory and its words are
//! `xmip-core-node`'s; a .NET program declares itself and the estate's
//! tooling lists the declarations through the runtime's library, which
//! forwards to the node and keeps nothing of its own. Declarations only; the
//! runtime's tests fail to compile if an export drifts from [`DeclareFn`] or
//! [`DeclarationsFn`].

use crate::ffi::Str;

/// `xmip_process_declare_v1`: declare the calling process.
pub const PROCESS_DECLARE_ENTRYPOINT: &str = "xmip_process_declare_v1";

/// `xmip_process_declarations_v1`: the declarations standing in a
/// directory, as JSON in memory only (ADR-0031 clause 2).
pub const PROCESS_DECLARATIONS_ENTRYPOINT: &str = "xmip_process_declarations_v1";

/// `name`, `location` and `purpose` (`test` or `runtime`), and
/// `properties_len` strings in `properties`, key then value. The file written
/// goes into `out` as UTF-8, its true byte length in `out_len`. `XMIP_OK`
/// with the file; `XMIP_E_INVALID` with the refusal in `out`; `XMIP_E_IO`
/// with the reason; `XMIP_E_MALFORMED` when a string is not UTF-8.
pub type DeclareFn = unsafe extern "C" fn(
    name: Str,
    location: Str,
    purpose: Str,
    properties: *const Str,
    properties_len: usize,
    out: *mut u8,
    cap: usize,
    out_len: *mut usize,
) -> i32;

/// `directory` empty for the directory the node names. The answer is written
/// into `out` as UTF-8, its true byte length in `out_len` whether or not it
/// fit. `XMIP_OK` with the answer; `XMIP_E_MALFORMED` when it is not UTF-8.
pub type DeclarationsFn =
    unsafe extern "C" fn(directory: Str, out: *mut u8, cap: usize, out_len: *mut usize) -> i32;
