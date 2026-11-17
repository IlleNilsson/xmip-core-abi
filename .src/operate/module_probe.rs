//! Header section 26: a module library, probed (ADR-0012, amendment
//! 2026-11-17). The opening and the judgment are the runtime loader's, the
//! one a node opens its modules with; every surface shows what it answers.
//! Declarations only; the runtime's tests fail to compile if the export
//! drifts from [`ModuleProbeFn`].

use crate::ffi::Str;

/// `xmip_module_probe_v1`.
pub const MODULE_PROBE_ENTRYPOINT: &str = "xmip_module_probe_v1";

/// `xmip_module_probe_v1`: the library's path in, what it declares, why it
/// could not be asked or why the host refuses it, and the lines it logged
/// out, as JSON in `out` as UTF-8, its true length in `out_len` whether or
/// not it fit.
pub type ModuleProbeFn =
    unsafe extern "C" fn(library: Str, out: *mut u8, cap: usize, out_len: *mut usize) -> i32;
