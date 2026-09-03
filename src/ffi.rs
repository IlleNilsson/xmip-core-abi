//! The wire form of the boundary: `#[repr(C)]` mirrors of `include/xmip_module.h`.
//!
//! ADR-0012 clause 1: the header is normative and this is not. Every layout,
//! name and number below follows the header, less the `Xmip` prefix where the
//! crate already supplies it — and where the two ever disagree, the header is
//! right and this is a defect. `ffi` because that is the recognised standard
//! name for exactly this layer, the same rule ADR-0011 applies to protocol
//! names.
//!
//! Declarations only. Nothing here dereferences a pointer, so nothing here is
//! `unsafe` — the crate's `deny(unsafe_code)` stands untouched. The one place
//! that legitimately crosses the boundary in this repository is the
//! conformance fixture in `examples/`, which lowers the lint at the top of the
//! file with its reason, exactly as the manifest comment says the vtable will.

/// Header section 3. `i32`, because that is what crosses the boundary.
pub mod status {
    pub const OK: i32 = 0;

    // Caller error. The call was wrong; repeating it unchanged will fail again.
    pub const INVALID: i32 = -1;
    pub const UNSUPPORTED: i32 = -2;
    pub const STATE: i32 = -3;
    pub const NOT_FOUND: i32 = -4;

    // Data. The input is at fault, not the caller and not the environment.
    pub const MALFORMED: i32 = -10;
    pub const CONTRACT: i32 = -11;
    pub const TRUNCATED: i32 = -12;

    // Environment.
    pub const IO: i32 = -20;
    pub const TIMEOUT: i32 = -21;
    pub const UNAVAILABLE: i32 = -22;
    pub const AUTH: i32 = -23;
    pub const CAPACITY: i32 = -24;

    // Control.
    pub const CANCELLED: i32 = -30;
    pub const AGAIN: i32 = -31;

    // Terminal. The module instance is unusable and must be destroyed.
    pub const INTERNAL: i32 = -40;
    pub const PANIC: i32 = -41;

    /// `XMIP_IS_RETRYABLE`. A property of the code, not of the call site.
    #[must_use]
    pub const fn is_retryable(code: i32) -> bool {
        matches!(code, TIMEOUT | UNAVAILABLE | CAPACITY | AGAIN)
    }

    /// `XMIP_IS_TERMINAL`. The instance is finished and must be destroyed.
    #[must_use]
    pub const fn is_terminal(code: i32) -> bool {
        matches!(code, INTERNAL | PANIC)
    }
}

/// A borrowed byte range. Header section 2: never owned by the receiver,
/// never null-terminated, valid only for the duration of the call it was
/// passed to. `ptr` may be null only when `len` is 0.
#[repr(C)]
#[derive(Clone, Copy)]
pub struct Slice {
    pub ptr: *const u8,
    pub len: usize,
}

/// A borrowed UTF-8 string; a [`Slice`] whose producer guarantees valid
/// UTF-8. `typedef XmipSlice XmipStr` in the header, a distinct type here
/// because Rust can afford the distinction the typedef could not.
#[repr(C)]
#[derive(Clone, Copy)]
pub struct Str {
    pub ptr: *const u8,
    pub len: usize,
}

impl Str {
    /// A borrow of a `'static` string — the one case where the "valid only
    /// for the call" rule is satisfied trivially, and the case a descriptor's
    /// names are expected to be.
    #[must_use]
    pub const fn from_static(text: &'static str) -> Self {
        Self {
            ptr: text.as_ptr(),
            len: text.len(),
        }
    }

    /// The empty string, which the header permits as `{ NULL, 0 }`.
    #[must_use]
    pub const fn empty() -> Self {
        Self {
            ptr: core::ptr::null(),
            len: 0,
        }
    }
}

/// Header section 4: what the module says it is, in wire form. The idiomatic
/// form is [`crate::descriptor::ModuleDescriptor`]; this is the one that
/// crosses.
#[repr(C)]
#[derive(Clone, Copy)]
pub struct WireDescriptor {
    pub abi_version: u32,
    pub provider: Str,
    pub module: Str,
    pub standard: Str,
    pub trait_major: u32,
    pub trait_minor: u32,
    pub module_major: u32,
    pub module_minor: u32,
    pub module_patch: u32,
}

/// Header section 6: what a module may call back into. Deliberately small —
/// no allocator, no thread pool, no clock, no configuration store.
#[repr(C)]
pub struct Host {
    pub abi_version: u32,
    pub ctx: *mut core::ffi::c_void,
    pub log:
        Option<unsafe extern "C" fn(ctx: *mut core::ffi::c_void, level: i32, target: Str, message: Str)>,
    pub cancelled: Option<unsafe extern "C" fn(ctx: *mut core::ffi::c_void) -> i32>,
    pub journey_id: Option<unsafe extern "C" fn(ctx: *mut core::ffi::c_void) -> Str>,
}

/// Header section 7: the module handle the entrypoint fills.
#[repr(C)]
pub struct Module {
    pub descriptor: WireDescriptor,
    pub state: *mut core::ffi::c_void,
    pub vtable: *const core::ffi::c_void,
    pub last_error: Option<unsafe extern "C" fn(state: *mut core::ffi::c_void) -> Str>,
    pub destroy: Option<unsafe extern "C" fn(state: *mut core::ffi::c_void)>,
}

/// `XmipCreateModuleFn`: the one exported symbol's signature, named
/// [`crate::XMIP_ENTRYPOINT`].
pub type CreateModuleFn = unsafe extern "C" fn(host: *const Host, out: *mut Module) -> i32;

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn the_status_numbers_are_the_headers_numbers() {
        // Spot-checked against the #defines rather than restated wholesale;
        // the grouping is what matters and the endpoints prove the groups.
        assert_eq!(status::OK, 0);
        assert_eq!(status::UNSUPPORTED, -2);
        assert_eq!(status::MALFORMED, -10);
        assert_eq!(status::CAPACITY, -24);
        assert_eq!(status::PANIC, -41);
    }

    #[test]
    fn retryable_matches_the_header_macro() {
        assert!(status::is_retryable(status::TIMEOUT));
        assert!(status::is_retryable(status::AGAIN));
        assert!(!status::is_retryable(status::IO), "IO faults repeat; the header says so");
        assert!(!status::is_retryable(status::PANIC));
    }

    #[test]
    fn a_static_str_borrows_correctly() {
        let s = Str::from_static("core");

        assert_eq!(s.len, 4);
        assert!(!s.ptr.is_null());
        assert!(Str::empty().ptr.is_null());
    }
}
