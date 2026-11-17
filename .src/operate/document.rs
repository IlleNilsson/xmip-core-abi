//! Header section 27: a configuration document read, written and one key
//! of it set (ADR-0031, amendment 2026-11-17). Every document a surface
//! reads or writes is read by `xmip-core-configure`, the runtime's one
//! reader, and crosses as JSON, in memory only. Declarations only; the
//! runtime's tests fail to compile if an export drifts from [`DocumentFn`].

/// `xmip_document_read_v1`: TOML text in, the document as JSON out.
pub const DOCUMENT_READ_ENTRYPOINT: &str = "xmip_document_read_v1";

/// `xmip_document_write_v1`: a JSON object in, the TOML text it writes out.
pub const DOCUMENT_WRITE_ENTRYPOINT: &str = "xmip_document_write_v1";

/// `xmip_document_set_v1`: TOML text and an edit in, the text with that
/// one key set or removed out, everything else as written.
pub const DOCUMENT_SET_ENTRYPOINT: &str = "xmip_document_set_v1";

/// The shape of section 10's exports, which these take too.
pub type DocumentFn = super::design::DesignFn;
