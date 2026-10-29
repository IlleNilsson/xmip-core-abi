//! Header section 24: a secret a Location presents, set through a node
//! reached at its operate listener, and the secrets it keeps listed, never
//! a value (ADR-0019, amendment 2026-10-29). Declarations only; the
//! runtime's tests fail to compile if an export drifts from them.

use crate::ffi::Str;
use crate::operate::remote::Remote;

/// `xmip_secret_v1`.
pub const SECRET_ENTRYPOINT: &str = "xmip_secret_v1";
/// `xmip_secrets_v1`.
pub const SECRETS_ENTRYPOINT: &str = "xmip_secrets_v1";

/// `xmip_secret_v1`: the secret `name` of `kind` set to `value` through
/// the node at `node`.
pub type SecretFn = unsafe extern "C" fn(
    remote: *const Remote,
    node: Str,
    name: Str,
    kind: Str,
    value: Str,
    said: *mut u8,
    cap: usize,
    said_len: *mut usize,
) -> i32;

/// `xmip_secrets_v1`: the secrets the node at `node` keeps, JSON.
pub type SecretsFn = unsafe extern "C" fn(
    remote: *const Remote,
    node: Str,
    out: *mut u8,
    cap: usize,
    out_len: *mut usize,
) -> i32;
