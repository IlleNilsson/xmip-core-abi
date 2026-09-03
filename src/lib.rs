//! The Rust binding over the Xmip module boundary.
//!
//! ADR-0012 clause 1: the normative boundary is `include/xmip_module.h` and the
//! specification beside it. This crate is clause 2 — a convenience for authors
//! who happen to write Rust. A module that declares the same `extern "C"`
//! signatures by hand is exactly as conformant, and nothing here is normative.
//!
//! Clause 4 draws the line this crate must not cross. `dyn Trait` has no stable
//! layout, so it never appears in a signature that reaches the header. The
//! traits below are Xmip-side ergonomics over a `#[repr(C)]` vtable.

pub mod descriptor;
pub mod ffi;
pub mod manifest;

pub use descriptor::{
    validate_module_abi, ModuleDescriptor, XMIP_ABI_VERSION, XMIP_ENTRYPOINT,
};
pub use manifest::{
    ExecutionHostKind, ExtensionEntrypoint, ExtensionManifest, HandlerInvocation, HandlerResult,
    HandlerStatus, ModuleCapability, ModuleEntrypoint, ModuleIdentity, ModuleManifest, XmipModule,
};
