//! Header section 11: Events, subscribed from any language (ADR-0065).
//!
//! A program subscribes with a filter and gets a handle, drains it or is
//! called back, and unsubscribes; it may publish an Event of its own. The
//! runtime's library forwards every call to `xmip-core-event` — the one
//! matching rule, the authorization gate, the hub and the audit are
//! there — and each language binds these shapes once. Declarations only;
//! the runtime's tests fail to compile if an export drifts from them.

use crate::ffi::Str;

use super::Scope;

/// `xmip_event_subscribe_v1`.
pub const SUBSCRIBE_ENTRYPOINT: &str = "xmip_event_subscribe_v1";
/// `xmip_event_next_v1`.
pub const NEXT_ENTRYPOINT: &str = "xmip_event_next_v1";
/// `xmip_event_batch_free_v1`.
pub const BATCH_FREE_ENTRYPOINT: &str = "xmip_event_batch_free_v1";
/// `xmip_event_listen_v1`.
pub const LISTEN_ENTRYPOINT: &str = "xmip_event_listen_v1";
/// `xmip_event_unsubscribe_v1`.
pub const UNSUBSCRIBE_ENTRYPOINT: &str = "xmip_event_unsubscribe_v1";
/// `xmip_event_publish_v1`.
pub const PUBLISH_ENTRYPOINT: &str = "xmip_event_publish_v1";

/// Header section 11, `XmipAction`: the stage whose action completed.
pub mod action {
    pub const RECEIVE: i32 = 0;
    pub const PROCESS: i32 = 1;
    pub const SEND: i32 = 2;
}

/// Header section 11, `XmipOutcome`: how it ended.
pub mod outcome {
    pub const SUCCESS: i32 = 0;
    pub const FAILURE: i32 = 1;
    pub const REJECTION: i32 = 2;
    pub const WAITING: i32 = 3;
    pub const PAUSE: i32 = 4;
    pub const TIMEOUT: i32 = 5;
    pub const EXHAUSTED_RETRIES: i32 = 6;
    pub const DISMISSAL: i32 = 7;
}

/// Header section 11, `XmipEvent`. Every string borrows from the batch it
/// came in, or from the caller for a publish; an identifier is a UUID in
/// 8-4-4-4-12 form, empty where there is none.
#[repr(C)]
#[derive(Clone, Copy)]
pub struct Event {
    pub id: Str,
    pub kind: Str,
    pub time_unix_nanos: i64,
    pub action: i32,
    pub outcome: i32,
    pub scope: Scope,
    pub journey: Str,
    pub message: Str,
    pub stream: Str,
    pub endpoint: Str,
    pub module: Str,
    pub artifact: Str,
    pub party: Str,
    /// `diagnostics_len` strings, name then value.
    pub diagnostics: *const Str,
    pub diagnostics_len: usize,
}

/// Header section 11, `XmipEventFilter`. An empty list or string is any.
#[repr(C)]
#[derive(Clone, Copy)]
pub struct EventFilter {
    pub types: *const Str,
    pub types_len: usize,
    pub outcomes: *const i32,
    pub outcomes_len: usize,
    pub scope: Scope,
    pub party: Str,
}

/// An open subscription, opaque.
#[repr(C)]
pub struct EventSubscription {
    _private: [u8; 0],
}

/// A drained batch of Events, opaque; the Events borrow from it.
#[repr(C)]
pub struct EventBatch {
    _private: [u8; 0],
}

/// `xmip_event_subscribe_v1`: `subscriber` (a Party's UUID) subscribes to
/// what `filter` asks for through a queue of `capacity` (0: the default);
/// its deliveries are audited as `program`'s, in `directory` when stated.
pub type SubscribeFn = unsafe extern "C" fn(
    program: Str,
    directory: Str,
    subscriber: Str,
    filter: *const EventFilter,
    capacity: usize,
    out: *mut *mut EventSubscription,
    said: *mut u8,
    said_cap: usize,
    said_len: *mut usize,
) -> i32;

/// `xmip_event_next_v1`: up to `max` Events, waiting up to `timeout_ms`.
pub type NextFn = unsafe extern "C" fn(
    subscription: *mut EventSubscription,
    timeout_ms: u32,
    max: usize,
    out_batch: *mut *mut EventBatch,
    out_events: *mut *const Event,
    out_len: *mut usize,
    out_refused: *mut u64,
) -> i32;

/// `xmip_event_batch_free_v1`.
pub type BatchFreeFn = unsafe extern "C" fn(batch: *mut EventBatch);

/// `XmipEventCallback`: one Event, valid for the call.
pub type Callback = unsafe extern "C" fn(context: *mut core::ffi::c_void, event: *const Event);

/// `xmip_event_listen_v1`: as subscribe, calling `callback` for each Event
/// on a thread of the runtime's.
pub type ListenFn = unsafe extern "C" fn(
    program: Str,
    directory: Str,
    subscriber: Str,
    filter: *const EventFilter,
    capacity: usize,
    callback: Callback,
    context: *mut core::ffi::c_void,
    out: *mut *mut EventSubscription,
    said: *mut u8,
    said_cap: usize,
    said_len: *mut usize,
) -> i32;

/// `xmip_event_unsubscribe_v1`.
pub type UnsubscribeFn = unsafe extern "C" fn(subscription: *mut EventSubscription);

/// `xmip_event_publish_v1`: an Event of the caller's, handed to every
/// matching subscription; how many took it.
pub type PublishFn = unsafe extern "C" fn(event: *const Event, out_delivered: *mut usize) -> i32;

#[cfg(test)]
mod tests {
    use super::*;

    const HEADER: &str = include_str!("../../include/xmip_operate.h");

    /// `NAME = value,` in the header, as an integer.
    fn enumerator(name: &str) -> i64 {
        HEADER
            .lines()
            .map(str::trim)
            .find_map(|line| {
                let rest = line.strip_prefix(name)?.trim_start().strip_prefix('=')?;
                rest.trim().trim_end_matches(',').parse().ok()
            })
            .unwrap_or_else(|| panic!("{name} is not in xmip_operate.h"))
    }

    #[test]
    fn every_entrypoint_matches_the_header() {
        for (define, name) in [
            ("XMIP_EVENT_SUBSCRIBE_ENTRYPOINT", SUBSCRIBE_ENTRYPOINT),
            ("XMIP_EVENT_NEXT_ENTRYPOINT", NEXT_ENTRYPOINT),
            ("XMIP_EVENT_BATCH_FREE_ENTRYPOINT", BATCH_FREE_ENTRYPOINT),
            ("XMIP_EVENT_LISTEN_ENTRYPOINT", LISTEN_ENTRYPOINT),
            ("XMIP_EVENT_UNSUBSCRIBE_ENTRYPOINT", UNSUBSCRIBE_ENTRYPOINT),
            ("XMIP_EVENT_PUBLISH_ENTRYPOINT", PUBLISH_ENTRYPOINT),
        ] {
            let line = HEADER
                .lines()
                .find(|line| line.starts_with(&format!("#define {define} ")))
                .unwrap_or_else(|| panic!("{define} is not in xmip_operate.h"));

            assert!(line.ends_with(&format!("\"{name}\"")), "{line}");
        }
    }

    #[test]
    fn every_action_and_outcome_matches_the_header() {
        for (name, value) in [
            ("XMIP_ACTION_RECEIVE", action::RECEIVE),
            ("XMIP_ACTION_PROCESS", action::PROCESS),
            ("XMIP_ACTION_SEND", action::SEND),
            ("XMIP_OUTCOME_SUCCESS", outcome::SUCCESS),
            ("XMIP_OUTCOME_FAILURE", outcome::FAILURE),
            ("XMIP_OUTCOME_REJECTION", outcome::REJECTION),
            ("XMIP_OUTCOME_WAITING", outcome::WAITING),
            ("XMIP_OUTCOME_PAUSE", outcome::PAUSE),
            ("XMIP_OUTCOME_TIMEOUT", outcome::TIMEOUT),
            ("XMIP_OUTCOME_EXHAUSTED_RETRIES", outcome::EXHAUSTED_RETRIES),
            ("XMIP_OUTCOME_DISMISSAL", outcome::DISMISSAL),
        ] {
            assert_eq!(i64::from(value), enumerator(name), "{name}");
        }
    }

    #[test]
    fn the_event_is_a_plain_c_layout() {
        // Ten strings, a time, two ints, a pointer and a length: what C
        // computes for the header's struct on a 64-bit or 32-bit target.
        let pointer = size_of::<usize>();
        let strings = 10 * size_of::<Str>();
        let ints = 8 + 4 + 4;
        assert_eq!(size_of::<Event>(), strings + ints + 2 * pointer);
        assert_eq!(size_of::<EventFilter>(), 2 * size_of::<Str>() + 4 * pointer);
    }
}
