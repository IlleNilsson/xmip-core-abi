//! Specification section 6: no panic crosses the boundary, either way.
//!
//! Every function a Rust library exports through the C boundary, and every
//! callback a Rust host puts in a table it hands a module, runs its body
//! through [`guarded`] at its outermost frame. A panic is caught there and
//! the function answers what its type says a failure with: `XMIP_E_PANIC`
//! for a status. Rust aborts the whole process where a panic reaches an
//! `extern "C"` frame, and the process is a PowerShell session, a GUI or a
//! node carrying other people's messages, so the guard is here once and
//! every export calls it.

use std::panic::{AssertUnwindSafe, catch_unwind};

/// `call` run so that no panic unwinds out of it: its answer, or
/// `panicked` where it panicked — `status::PANIC` for a function answering
/// a status, terminal by section 6, so its caller destroys what answered it.
///
/// `call` is asserted unwind-safe: whatever it touched is the instance
/// section 6 declares unusable once it has panicked.
pub fn guarded<T>(panicked: T, call: impl FnOnce() -> T) -> T {
    catch_unwind(AssertUnwindSafe(call)).unwrap_or(panicked)
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::ffi::status;

    #[test]
    fn a_call_that_answers_is_answered_and_one_that_panics_answers_panic() {
        assert_eq!(guarded(status::PANIC, || status::OK), status::OK);
        assert_eq!(
            guarded(status::PANIC, || -> i32 { panic!("a defect") }),
            status::PANIC
        );
        assert!(!guarded(false, || -> bool { panic!("a defect") }));
    }
}
