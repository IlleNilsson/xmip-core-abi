# xmip-core-abi

The Xmip application binary interface (ABI): the stable boundary used by the runtime, operator surfaces, and loadable Modules.

The C header and its specification are normative. The Rust crate is a convenience binding over that boundary; it must not introduce Rust-specific types into the ABI.

Status: planned, with the Rust binding and module manifest model already present.
