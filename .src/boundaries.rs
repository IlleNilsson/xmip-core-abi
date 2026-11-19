//! The two boundaries this build speaks, said at once: the module boundary
//! a Module plugs into — its version, its entrypoint and how this platform
//! names a module's library — and the operator boundary a surface drives
//! from (ADR-0027 clause 2, versioned apart). `xmip abi` renders it and
//! `Get-XmipAbi` emits it. Until 2026-11-17 it was the .NET binding's alone.

use crate::descriptor::{XMIP_ABI_VERSION, XMIP_ENTRYPOINT};
use crate::operate::{XMIP_OPERATE_ENTRYPOINT, XMIP_OPERATE_VERSION};

/// The module a library file name is shown for.
pub const EXAMPLE_MODULE: &str = "xmip_core_transport_file";

/// Both boundaries.
#[derive(Clone, Debug, PartialEq, Eq)]
pub struct Boundaries {
    pub module_version: u32,
    pub module_entrypoint: &'static str,
    /// How this platform names [`EXAMPLE_MODULE`]'s library.
    pub module_library_file_name: String,
    pub operate_version: u32,
    pub operate_entrypoint: &'static str,
}

impl Boundaries {
    /// The boundaries of this build.
    #[must_use]
    pub fn current() -> Self {
        Self {
            module_version: XMIP_ABI_VERSION,
            module_entrypoint: XMIP_ENTRYPOINT,
            module_library_file_name: library_file_name(EXAMPLE_MODULE),
            operate_version: XMIP_OPERATE_VERSION,
            operate_entrypoint: XMIP_OPERATE_ENTRYPOINT,
        }
    }
}

/// How this platform names the loadable library of the module `module`
/// (the header, section 1): its prefix where it has one, its suffix always.
#[must_use]
pub fn library_file_name(module: &str) -> String {
    format!(
        "{}{module}{}",
        std::env::consts::DLL_PREFIX,
        std::env::consts::DLL_SUFFIX
    )
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn the_boundaries_are_this_build_s_and_a_library_is_named_for_its_platform() {
        let current = Boundaries::current();

        assert_eq!(current.module_entrypoint, "xmip_create_module_v1");
        assert_eq!(current.operate_entrypoint, "xmip_operate_v1");
        let named = library_file_name("m");
        if cfg!(windows) {
            assert_eq!(named, "m.dll");
        } else if cfg!(target_os = "macos") {
            assert_eq!(named, "libm.dylib");
        } else {
            assert_eq!(named, "libm.so");
        }
    }
}
