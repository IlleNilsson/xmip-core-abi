//! Header section 9: a program's audit record, crossed once.
//!
//! Every Xmip program audits through `xmip-core-audit` (ADR-0062); a .NET
//! program and PowerShell reach it through the runtime's library, whose
//! `xmip_audit_v1` forwards to the capability and writes no record of its
//! own. Declarations only; the runtime's tests fail to compile if its export
//! drifts from [`AuditFn`].

use crate::ffi::Str;

/// `xmip_audit_v1`.
pub const AUDIT_ENTRYPOINT: &str = "xmip_audit_v1";

/// `XMIP_EVENT_SOURCE`: the Windows Event Log source every Xmip entry is
/// written under when audit cannot persist a record (ADR-0062 clause 3).
pub const EVENT_SOURCE: &str = "Xmip";

/// `XMIP_EVENT_SOURCE_UNREGISTERED`: the sentence an entry opens with when
/// [`EVENT_SOURCE`] is not registered and the entry goes under `.NET Runtime`.
pub const EVENT_SOURCE_UNREGISTERED: &str = "The Xmip event source is not registered and \
     registering it needs elevation once (Install-XmipPrerequisite does it), so this is \
     written under the .NET Runtime source.";

/// Header section 9, `XmipPhase`, as it crosses: the lifecycle phase.
pub mod phase {
    pub const BEGIN: i32 = 0;
    pub const EXECUTE: i32 = 1;
    pub const FINISHED: i32 = 2;
    pub const FAILURE: i32 = 3;
}

/// Header section 9, `XmipSeverity`, as it crosses.
pub mod severity {
    pub const INFORMATION: i32 = 0;
    pub const WARNING: i32 = 1;
    pub const ERROR: i32 = 2;
}

/// Header section 9, `XmipKept`: what became of the record.
pub mod kept {
    pub const SUPPRESSED: i32 = 0;
    pub const PERSISTED: i32 = 1;
    pub const OPERATING_SYSTEM: i32 = 2;
}

/// `xmip_audit_v1`: one record of what `program` did. `properties` holds
/// `properties_len` strings, key then value. What became of it is written to
/// `out_kept`; where it went and why, when it did not go to the sink, into
/// `said` as UTF-8, its true length in `said_len`.
pub type AuditFn = unsafe extern "C" fn(
    program: Str,
    directory: Str,
    action: Str,
    phase: i32,
    severity: i32,
    message: Str,
    properties: *const Str,
    properties_len: usize,
    out_kept: *mut i32,
    said: *mut u8,
    said_cap: usize,
    said_len: *mut usize,
) -> i32;
