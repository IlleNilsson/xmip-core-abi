//! Header section 13: a System Process, declared (ADR-0053 clause 3).
//!
//! The declaration's words are `xmip-core-node`'s, its file, its directory
//! and its reading `xmip-core-runtime`'s; a .NET program declares itself and
//! takes its declaration away, and the estate's tooling lists the
//! declarations and takes away those whose process is gone, through the
//! runtime's library, keeping nothing of their own. Declarations only; the
//! runtime's tests fail to compile if an export drifts from [`DeclareFn`],
//! [`DeclarationsFn`] or [`UndeclareFn`].

use crate::ffi::Str;

/// `xmip_process_declare_v1`: declare the calling process.
pub const PROCESS_DECLARE_ENTRYPOINT: &str = "xmip_process_declare_v1";

/// `xmip_process_declarations_v1`: the declarations standing in a
/// directory, as JSON in memory only (ADR-0031 clause 2).
pub const PROCESS_DECLARATIONS_ENTRYPOINT: &str = "xmip_process_declarations_v1";

/// `xmip_process_undeclare_v1`: take a declaration away.
pub const PROCESS_UNDECLARE_ENTRYPOINT: &str = "xmip_process_undeclare_v1";

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

/// `file`, one a declaration stands in (`xmip-*.toml`), taken away; one
/// already gone is as good as removed. `XMIP_OK`; `XMIP_E_INVALID` with the
/// refusal in `out` when `file` is no declaration's, which is left where it
/// is; `XMIP_E_IO` with the reason; `XMIP_E_MALFORMED` when it is not UTF-8.
/// Whatever is written into `out` has its true byte length in `out_len`.
pub type UndeclareFn =
    unsafe extern "C" fn(file: Str, out: *mut u8, cap: usize, out_len: *mut usize) -> i32;
