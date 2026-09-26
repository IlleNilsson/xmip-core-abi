# SPDX-License-Identifier: AGPL-3.0-or-later
# Copyright the Xmip authors.

"""xmip_event through the runtime's real library.

The binding's numbers and names against include/xmip_operate.h, a
subscription that drains what it matches, a listener called back, and an
Event that reaches its subscriber within a millisecond of its publish.

    XMIP_RUNTIME_LIBRARY=<library> XMIP_EVENT_AUDIT_DIRECTORY=<directory> \\
        python -m unittest test_xmip_event

The audit directory is never empty here: an empty one would audit to the
operating system's log.
"""

from __future__ import annotations

import os
import pathlib
import queue
import re
import statistics
import threading
import time
import unittest

import xmip_event
from xmip_event import Action, Event, EventError, Filter, Library, Outcome

SUBSCRIBER = "0198a3c4-0000-7000-8000-000000000042"
PROGRAM = "xmip-core-abi python test"
WARMUP = 100
MEASURED = 500
INCLUDE = pathlib.Path(__file__).resolve().parent.parent / "include"


def raised(scope: str, outcome: Outcome, **more) -> Event:
    return Event(type="se.xmip.receive.failure", action=Action.RECEIVE, outcome=outcome,
                 scope=scope, artifact="orders", **more)


class TheHeaderIsTheBindings(unittest.TestCase):
    """The binding's copy of the header's numbers is held to the header."""

    operate = (INCLUDE / "xmip_operate.h").read_text(encoding="utf-8")
    module = (INCLUDE / "xmip_module.h").read_text(encoding="utf-8")

    def numbers(self, prefix: str) -> dict[str, int]:
        found = re.findall(prefix + r"(\w+)\s*=\s*(\d+)", self.operate)
        return {name: int(value) for name, value in found}

    def test_outcomes_are_the_headers(self) -> None:
        self.assertEqual({o.name: o.value for o in Outcome}, self.numbers("XMIP_OUTCOME_"))

    def test_actions_are_the_headers(self) -> None:
        self.assertEqual({a.name: a.value for a in Action}, self.numbers("XMIP_ACTION_"))

    def test_entrypoints_are_the_headers(self) -> None:
        found = re.findall(r'#define XMIP_EVENT_(\w+)_ENTRYPOINT\s+"(\w+)"', self.operate)
        self.assertEqual(xmip_event.ENTRYPOINTS, dict(found))

    def test_statuses_are_the_headers(self) -> None:
        self.assertRegex(self.module, rf"#define XMIP_OK\s+{xmip_event.OK}\b")
        self.assertRegex(self.module, rf"#define XMIP_E_TIMEOUT\s+\({xmip_event.E_TIMEOUT}\)")


class ThroughTheRuntime(unittest.TestCase):
    """Subscribed, drained and called back through the runtime's library."""

    @classmethod
    def setUpClass(cls) -> None:
        path = os.environ.get("XMIP_RUNTIME_LIBRARY", "")
        cls.directory = os.environ.get("XMIP_EVENT_AUDIT_DIRECTORY", "")
        if not path or not cls.directory:
            raise unittest.SkipTest("XMIP_RUNTIME_LIBRARY and XMIP_EVENT_AUDIT_DIRECTORY")
        cls.xmip = Library(path)

    def subscribe(self, filter: Filter):
        return self.xmip.subscribe(PROGRAM, self.directory, SUBSCRIBER, filter)

    def test_a_subscriber_is_a_party(self) -> None:
        with self.assertRaises(EventError) as refused:
            self.xmip.subscribe(PROGRAM, self.directory, "")
        self.assertEqual(refused.exception.status, -1, "XMIP_E_INVALID")

    def test_a_subscription_drains_what_it_matches(self) -> None:
        scope = "xmip:///python-drain/node/n1/receive/orders"
        failures = Filter(outcomes=(Outcome.FAILURE,), scope="xmip:///python-drain")
        with self.subscribe(failures) as subscription:
            said = {"status": "refused"}
            self.assertEqual(self.xmip.publish(raised(scope, Outcome.FAILURE,
                                                      diagnostics=said)), 1)
            self.assertEqual(self.xmip.publish(raised(scope, Outcome.SUCCESS)), 0)
            delivery = subscription.next(timeout_ms=1000)
            self.assertIsNotNone(delivery)
            self.assertEqual((len(delivery.events), delivery.refused), (1, 0))
            event = delivery.events[0]
            self.assertEqual(event.type, "se.xmip.receive.failure")
            self.assertEqual(event.scope, scope)
            self.assertEqual(event.artifact, "orders")
            self.assertEqual(event.outcome, Outcome.FAILURE)
            self.assertEqual(len(event.id), 36, "minted")
            self.assertGreater(event.time_unix_nanos, 0, "now")
            self.assertEqual(event.diagnostics, said)
            self.assertIsNone(subscription.next(timeout_ms=1), "an empty queue times out")

    def test_a_listening_subscription_is_never_drained(self) -> None:
        with self.xmip.listen(PROGRAM, self.directory, SUBSCRIBER,
                              Filter(scope="xmip:///python-state"), lambda event: None) as heard:
            with self.assertRaises(EventError) as refused:
                heard.next(timeout_ms=1)
            self.assertEqual(refused.exception.status, -3, "XMIP_E_STATE")

    def test_a_drained_event_arrives_within_a_millisecond(self) -> None:
        timing = Timing()
        stop = threading.Event()
        with self.subscribe(Filter(scope="xmip:///python-latency")) as subscription:
            def drain() -> None:
                while not stop.is_set():
                    delivery = subscription.next(timeout_ms=100, max=16)
                    if delivery:
                        when = time.perf_counter_ns()
                        for _ in delivery.events:
                            timing.arrived(when)
            receiver = threading.Thread(target=drain)
            receiver.start()
            try:
                self.measure(timing, "xmip:///python-latency/node/n1", "next")
            finally:
                stop.set()
                receiver.join()

    def test_a_listener_is_called_back_within_a_millisecond(self) -> None:
        timing = Timing()
        seen: list[Event] = []

        def called(event: Event) -> None:
            timing.arrived(time.perf_counter_ns())
            if not seen:
                seen.append(event)
        with self.xmip.listen(PROGRAM, self.directory, SUBSCRIBER,
                              Filter(scope="xmip:///python-listen"), called):
            self.measure(timing, "xmip:///python-listen/node/n1", "callback")
        self.assertEqual(seen[0].scope, "xmip:///python-listen/node/n1")

    def measure(self, timing: Timing, scope: str, path: str) -> None:
        """Publish one Event and wait for it, then wake a plain thread on a
        plain queue and wait for that, round after round. The rule is a
        millisecond apart from load: each bound is 1 ms plus the plain wake's."""
        plain, woken, sent = Timing(), [], []
        told: queue.SimpleQueue[int] = queue.SimpleQueue()

        def wait() -> None:
            for _ in range(WARMUP + MEASURED):
                told.get()
                plain.arrived(time.perf_counter_ns())
        waiter = threading.Thread(target=wait)
        waiter.start()
        event = raised(scope, Outcome.FAILURE)
        try:
            for i in range(WARMUP + MEASURED):
                sent.append(time.perf_counter_ns())
                self.assertEqual(self.xmip.publish(event), 1)
                self.assertTrue(timing.awaited(), "an Event arrived")
                woken.append(time.perf_counter_ns())
                told.put(i)
                self.assertTrue(plain.awaited(), "the plain thread woke")
        finally:
            for _ in range(WARMUP + MEASURED):
                told.put(-1)
            waiter.join()
        median, p99 = spread(f"publish to {path}", sent, timing.at)
        usual, tail = spread("a plain wake beside it", woken, plain.at)
        self.assertLess(median, 1000.0 + usual, f"{path}: median")
        self.assertLess(p99, 1000.0 + tail, f"{path}: p99")


class Timing:
    """Arrivals: when each one was seen, and how many have been."""

    def __init__(self) -> None:
        self.at: list[int] = []
        self.received = threading.Semaphore(0)

    def arrived(self, when: int) -> None:
        self.at.append(when)
        self.received.release()

    def awaited(self) -> bool:
        return self.received.acquire(timeout=2.0)


def spread(what: str, sent: list[int], at: list[int]) -> tuple[float, float]:
    """Median, p99 and worst in microseconds, printed; the first two returned."""
    micros = sorted((a - s) / 1000.0 for s, a in zip(sent[WARMUP:], at[WARMUP:]))
    median, p99 = statistics.median(micros), micros[len(micros) * 99 // 100]
    print(f"\n        {what:<26} median {median:8.1f} us, p99 {p99:8.1f} us, "
          f"worst {micros[-1]:8.1f} us over {len(micros)}", end="")
    return median, p99


if __name__ == "__main__":
    unittest.main()
