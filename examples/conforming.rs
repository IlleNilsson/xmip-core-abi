//! A conforming module, as small as conformance allows.
//!
//! This is the fixture the probes load: `xmip probe` in xmip-core-cli and
//! `Get-XmipModuleDescriptor` in xmip-core-powershell both exercise the first
//! of the seven conformance rules in section 11 of the header against exactly
//! this artifact — the module exports the entrypoint, refuses a foreign
//! `abi_version` with `XMIP_E_UNSUPPORTED`, fills the descriptor, and
//! destroys cleanly.
//!
//! It lives in the repository that owns the boundary because it is executable
//! proof of the header, not a product: a fixture that drifted from the
//! contract would fail the probes, which is the point of having it.
//!
//! Build and probe:
//!
//! ```text
//! cargo build --example conforming
//! xmip probe target/debug/examples/conforming.dll
//! ```
#![allow(
    unsafe_code,
    reason = "the one purpose of this file is to stand on the far side of the \
              C boundary; the manifest's deny is lowered here exactly as its \
              own comment says the vtable eventually will"
)]

use xmip_core_abi::XMIP_ABI_VERSION;
use xmip_core_abi::ffi::{Host, Module, Str, WireDescriptor, status};

/// The one exported symbol, named by `XMIP_ENTRYPOINT`.
///
/// # Safety
///
/// Called by a host across the C boundary. `host` and `out` must be valid for
/// the duration of the call; the header owns that contract and this side
/// checks what it can — a null pointer or a foreign version is refused, never
/// dereferenced into.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn xmip_create_module_v1(host: *const Host, out: *mut Module) -> i32 {
    if host.is_null() || out.is_null() {
        return status::INVALID;
    }

    // Section 7: a module that cannot support the host's abi_version returns
    // XMIP_E_UNSUPPORTED here, leaving *out untouched, rather than failing
    // later.
    let spoken = unsafe { (*host).abi_version };

    if spoken != XMIP_ABI_VERSION {
        return status::UNSUPPORTED;
    }

    unsafe {
        *out = Module {
            descriptor: WireDescriptor {
                abi_version: XMIP_ABI_VERSION,
                provider: Str::from_static("core"),
                module: Str::from_static("conformance"),
                // Empty is legal only for the core provider, which this is.
                standard: Str::empty(),
                trait_major: 1,
                trait_minor: 0,
                module_major: 0,
                module_minor: 1,
                module_patch: 0,
            },
            // No state: everything this module says is 'static, so there is
            // nothing to allocate and nothing destroy has to free.
            state: core::ptr::null_mut(),
            // No vtable: conformance is a descriptor and a clean lifecycle,
            // not a capability. The host selects trait tables by descriptor
            // name, and "conformance" names none.
            vtable: core::ptr::null(),
            last_error: Some(last_error),
            destroy: Some(destroy),
        };
    }

    status::OK
}

/// Section 7: detail for the most recent failing call. Nothing here can fail,
/// so the answer is the empty string, which the header makes legal.
unsafe extern "C" fn last_error(_state: *mut core::ffi::c_void) -> Str {
    Str::empty()
}

/// Section 7: the module frees its own state. This one has none, and a no-op
/// destroy is still a destroy — the host must be able to call it exactly once
/// without consequence.
unsafe extern "C" fn destroy(_state: *mut core::ffi::c_void) {}
