# SPDX-License-Identifier: AGPL-3.0-or-later
# Copyright the Xmip authors.

"""Xmip's Events, subscribed from Python over ctypes (xmip_operate.h section 11).

The runtime's library is loaded by path and its six symbols looked up once by
the names the header gives them. Thin (ADR-0065): which Events a filter
matches, whether a subscriber may see them, the queue and the audit are the
runtime's. Events are copied out of the batch or callback they arrived in.
"""

from __future__ import annotations

import ctypes
import enum
from dataclasses import dataclass, field

__all__ = [
    "Action", "Delivery", "Event", "EventError", "Filter", "Library", "Outcome",
    "Subscription", "ENTRYPOINTS",
]

#: The header's XMIP_EVENT_*_ENTRYPOINT names, by what follows XMIP_EVENT_.
ENTRYPOINTS = {
    "SUBSCRIBE": "xmip_event_subscribe_v1",
    "NEXT": "xmip_event_next_v1",
    "BATCH_FREE": "xmip_event_batch_free_v1",
    "LISTEN": "xmip_event_listen_v1",
    "UNSUBSCRIBE": "xmip_event_unsubscribe_v1",
    "PUBLISH": "xmip_event_publish_v1",
}

#: XMIP_OK, XMIP_E_TIMEOUT: xmip_module.h section 3.
OK = 0
E_TIMEOUT = -21


class Action(enum.IntEnum):
    """XmipAction: the stage whose action completed."""

    RECEIVE = 0
    PROCESS = 1
    SEND = 2


class Outcome(enum.IntEnum):
    """XmipOutcome: how an action ended."""

    SUCCESS = 0
    FAILURE = 1
    REJECTION = 2
    WAITING = 3
    PAUSE = 4
    TIMEOUT = 5
    EXHAUSTED_RETRIES = 6
    DISMISSAL = 7


class EventError(Exception):
    """A status other than XMIP_OK, with what the runtime said about it."""

    def __init__(self, status: int, said: str = "") -> None:
        super().__init__(said or f"Xmip status {status}")
        self.status = status


@dataclass(frozen=True)
class Event:
    """XmipEvent: references, never a payload. An empty id is minted and a
    time of 0 is now when published; every optional string is empty where
    there is none."""

    type: str
    action: Action
    outcome: Outcome
    scope: str
    id: str = ""
    time_unix_nanos: int = 0
    journey: str = ""
    message: str = ""
    stream: str = ""
    endpoint: str = ""
    module: str = ""
    artifact: str = ""
    party: str = ""
    diagnostics: dict[str, str] = field(default_factory=dict)


@dataclass(frozen=True)
class Filter:
    """XmipEventFilter: every empty list or string is any."""

    types: tuple[str, ...] = ()
    outcomes: tuple[Outcome, ...] = ()
    scope: str = ""
    party: str = ""


@dataclass(frozen=True)
class Delivery:
    """One drain: the Events, and how many a full queue refused since the last."""

    events: list[Event]
    refused: int


class _Str(ctypes.Structure):
    _fields_ = [("ptr", ctypes.c_void_p), ("len", ctypes.c_size_t)]


_TEXTS = ("journey", "message", "stream", "endpoint", "module", "artifact", "party")
_SET = ("id", "type", "scope", *_TEXTS)


class _Event(ctypes.Structure):
    _fields_ = [
        ("id", _Str), ("type", _Str), ("time_unix_nanos", ctypes.c_int64),
        ("action", ctypes.c_int32), ("outcome", ctypes.c_int32), ("scope", _Str),
        *[(name, _Str) for name in _TEXTS],
        ("diagnostics", ctypes.POINTER(_Str)), ("diagnostics_len", ctypes.c_size_t),
    ]


class _Filter(ctypes.Structure):
    _fields_ = [
        ("types", ctypes.POINTER(_Str)), ("types_len", ctypes.c_size_t),
        ("outcomes", ctypes.POINTER(ctypes.c_int32)), ("outcomes_len", ctypes.c_size_t),
        ("scope", _Str), ("party", _Str),
    ]


_CALLBACK = ctypes.CFUNCTYPE(None, ctypes.c_void_p, ctypes.POINTER(_Event))
_P = ctypes.c_void_p
_SIZE = ctypes.c_size_t
_SIZE_P = ctypes.POINTER(ctypes.c_size_t)
_SHAPES = {
    "SUBSCRIBE": (ctypes.c_int32, [_Str, _Str, _Str, ctypes.POINTER(_Filter), _SIZE,
                                   ctypes.POINTER(_P), _P, _SIZE, _SIZE_P]),
    "NEXT": (ctypes.c_int32, [_P, ctypes.c_uint32, _SIZE, ctypes.POINTER(_P),
                              ctypes.POINTER(ctypes.POINTER(_Event)), _SIZE_P,
                              ctypes.POINTER(ctypes.c_uint64)]),
    "BATCH_FREE": (None, [_P]),
    "LISTEN": (ctypes.c_int32, [_Str, _Str, _Str, ctypes.POINTER(_Filter), _SIZE, _CALLBACK,
                                _P, ctypes.POINTER(_P), _P, _SIZE, _SIZE_P]),
    "UNSUBSCRIBE": (None, [_P]),
    "PUBLISH": (ctypes.c_int32, [ctypes.POINTER(_Event), _SIZE_P]),
}


def _text(value: _Str) -> str:
    return ctypes.string_at(value.ptr, value.len).decode("utf-8") if value.len else ""


class _Keep:
    """Encoded strings kept alive for the call they are borrowed by."""

    def __init__(self) -> None:
        self.kept: list[ctypes.c_char_p] = []

    def str(self, text: str) -> _Str:
        if not text:
            return _Str(None, 0)
        # The pointer into the encoded bytes, read without a foreign call:
        # every publish marshals a dozen of these.
        data = text.encode("utf-8")
        held = ctypes.c_char_p(data)
        self.kept.append(held)
        return _Str(ctypes.c_void_p.from_buffer(held).value, len(data))


def _copied(event: _Event) -> Event:
    pairs = [_text(event.diagnostics[i]) for i in range(event.diagnostics_len)]
    return Event(
        type=_text(event.type), action=Action(event.action), outcome=Outcome(event.outcome),
        scope=_text(event.scope), id=_text(event.id), time_unix_nanos=event.time_unix_nanos,
        **{name: _text(getattr(event, name)) for name in _TEXTS},
        diagnostics=dict(zip(pairs[0::2], pairs[1::2])),
    )


class Subscription:
    """A subscription the runtime holds, drained or listening. Closing it
    unsubscribes; for a listening one, once the callback in progress returned."""

    def __init__(self, library: Library, handle: ctypes.c_void_p, callback=None) -> None:
        self._library = library
        self._handle = handle
        self._callback = callback

    def next(self, timeout_ms: int, max: int = 64) -> Delivery | None:
        """Up to max Events, waiting up to timeout_ms for the first; None when
        none arrived. The wait ends when an Event does, not on a timer."""
        batch, events = ctypes.c_void_p(), ctypes.POINTER(_Event)()
        length, refused = ctypes.c_size_t(), ctypes.c_uint64()
        status = self._library._fn["NEXT"](
            self._handle, timeout_ms, max, ctypes.byref(batch), ctypes.byref(events),
            ctypes.byref(length), ctypes.byref(refused))
        if status == E_TIMEOUT:
            return None
        if status != OK:
            raise EventError(status)
        try:
            copied = [_copied(events[i]) for i in range(length.value)]
            return Delivery(copied, refused.value)
        finally:
            self._library._fn["BATCH_FREE"](batch)

    def close(self) -> None:
        """Unsubscribe; nothing is delivered afterwards. Idempotent."""
        if self._handle:
            self._library._fn["UNSUBSCRIBE"](self._handle)
            self._handle = None
            self._callback = None

    def __enter__(self) -> Subscription:
        return self

    def __exit__(self, *_: object) -> None:
        self.close()


class Library:
    """The runtime's library, loaded by path. Outlives every Subscription."""

    def __init__(self, path: str) -> None:
        self._dll = ctypes.CDLL(path)
        self._fn = {}
        for name, (result, arguments) in _SHAPES.items():
            try:
                function = getattr(self._dll, ENTRYPOINTS[name])
            except AttributeError:
                raise EventError(-4, f"the library has no {ENTRYPOINTS[name]}") from None
            function.restype, function.argtypes = result, arguments
            self._fn[name] = function

    def subscribe(self, program: str, directory: str, subscriber: str,
                  filter: Filter = Filter(), capacity: int = 0) -> Subscription:
        """Subscribe to drain. program and directory say where it is audited
        (an empty directory: the capability decides); subscriber is the
        Party's UUID. Raises EventError with the gate's sentence when refused."""
        return self._open(program, directory, subscriber, filter, capacity, None)

    def listen(self, program: str, directory: str, subscriber: str, filter: Filter,
               callback, capacity: int = 0) -> Subscription:
        """Subscribe to have callback(event) called on a thread the runtime
        starts, one call at a time. An exception it raises is printed by
        ctypes and goes no further."""
        def called(_context, event):
            callback(_copied(event.contents))
        return self._open(program, directory, subscriber, filter, capacity, _CALLBACK(called))

    def publish(self, event: Event) -> int:
        """Hand event to every matching subscription; how many queues took it."""
        keep = _Keep()
        wire = _Event(time_unix_nanos=event.time_unix_nanos, action=int(event.action),
                      outcome=int(event.outcome))
        # Only what is there: the struct starts zeroed, and an empty string
        # is a zeroed XmipStr. Every publish marshals this.
        for name in _SET:
            text = getattr(event, name)
            if text:
                setattr(wire, name, keep.str(text))
        if event.diagnostics:
            pairs = [keep.str(text) for kv in event.diagnostics.items() for text in kv]
            wire.diagnostics = (_Str * len(pairs))(*pairs)
            wire.diagnostics_len = len(pairs)
        delivered = ctypes.c_size_t()
        status = self._fn["PUBLISH"](ctypes.byref(wire), ctypes.byref(delivered))
        if status != OK:
            raise EventError(status)
        return delivered.value

    def _open(self, program, directory, subscriber, filter, capacity, callback):
        keep = _Keep()
        types = [keep.str(text) for text in filter.types]
        outcomes = [int(outcome) for outcome in filter.outcomes]
        wire = _Filter(
            types=(_Str * len(types))(*types), types_len=len(types),
            outcomes=(ctypes.c_int32 * len(outcomes))(*outcomes),
            outcomes_len=len(outcomes),
            scope=keep.str(filter.scope), party=keep.str(filter.party),
        )
        handle = ctypes.c_void_p()
        said = ctypes.create_string_buffer(512)
        said_len = ctypes.c_size_t()
        who = (keep.str(program), keep.str(directory), keep.str(subscriber))
        if callback is None:
            status = self._fn["SUBSCRIBE"](*who, ctypes.byref(wire), capacity,
                                           ctypes.byref(handle), said, len(said),
                                           ctypes.byref(said_len))
        else:
            status = self._fn["LISTEN"](*who, ctypes.byref(wire), capacity, callback, None,
                                        ctypes.byref(handle), said, len(said),
                                        ctypes.byref(said_len))
        if status != OK:
            raise EventError(status, said.raw[:said_len.value].decode("utf-8", "replace"))
        return Subscription(self, handle, callback)
