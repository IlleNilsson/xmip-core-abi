# SPDX-License-Identifier: AGPL-3.0-or-later
# Copyright the Xmip authors.

"""xmip_event through the runtime's real library.

The binding's numbers and names against include/xmip_operate.h, and a
program subscribing that names no Party of its own: it is the account the
operating system says it runs as, resolved through the Parties of the node
serving in its process (ADR-0019, amendment 2026-11-11). This test's process
serves no node, so subscribe and listen are refused XMIP_E_AUTH with the
sentence, and publish, the policy and who is unheard still cross.

    XMIP_RUNTIME_LIBRARY=<library> XMIP_EVENT_AUDIT_DIRECTORY=<directory> \\
        python -m unittest test_xmip_event

The audit directory is never empty here: an empty one would audit to the
operating system's log.
"""

from __future__ import annotations

import os
import pathlib
import re
import unittest

import xmip_event
from xmip_event import Action, Event, EventError, Filter, Library, Outcome

PROGRAM = "xmip-core-abi python test"
NO_PARTY = "is no Party of a node serving in this process"
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
        # Every symbol the binding looks up is the header's by the same name;
        # the header's operator symbols (list and act) are the surfaces'.
        found = dict(re.findall(r'#define XMIP_EVENT_(\w+)_ENTRYPOINT\s+"(\w+)"', self.operate))
        self.assertTrue(xmip_event.ENTRYPOINTS)
        self.assertEqual(xmip_event.ENTRYPOINTS,
                         {name: found.get(name) for name in xmip_event.ENTRYPOINTS})

    def test_statuses_are_the_headers(self) -> None:
        self.assertRegex(self.module, rf"#define XMIP_OK\s+{xmip_event.OK}\b")
        self.assertRegex(self.module, rf"#define XMIP_E_TIMEOUT\s+\({xmip_event.E_TIMEOUT}\)")



class ThroughTheRuntime(unittest.TestCase):
    """Refused, published and asked through the runtime's library."""

    @classmethod
    def setUpClass(cls) -> None:
        path = os.environ.get("XMIP_RUNTIME_LIBRARY", "")
        cls.directory = os.environ.get("XMIP_EVENT_AUDIT_DIRECTORY", "")
        if not path or not cls.directory:
            raise unittest.SkipTest("XMIP_RUNTIME_LIBRARY and XMIP_EVENT_AUDIT_DIRECTORY")
        cls.xmip = Library(path)
        # Whatever the policy allows, a subscriber must prove to be a Party.
        cls.xmip.authorize_by(lambda *_: True)

    def test_a_program_subscribing_names_no_party_of_its_own(self) -> None:
        here = Filter(scope="xmip:///python-proven")
        with self.assertRaises(EventError) as refused:
            self.xmip.subscribe(PROGRAM, self.directory, here)
        self.assertEqual(refused.exception.status, -23, "XMIP_E_AUTH")
        self.assertIn(NO_PARTY, str(refused.exception))
        with self.assertRaises(EventError) as refused:
            self.xmip.listen(PROGRAM, self.directory, here, lambda event: None)
        self.assertEqual(refused.exception.status, -23, "XMIP_E_AUTH")

    def test_an_event_published_here_crosses_whole(self) -> None:
        scope = "xmip:///python-proven/node/n1/receive/orders"
        self.assertEqual(self.xmip.publish(raised(scope, Outcome.FAILURE,
                                                  diagnostics={"status": "refused"})), 0)
        with self.assertRaises(EventError) as refused:
            self.xmip.publish(raised(scope, Outcome.FAILURE, journey="not a uuid"))
        self.assertEqual(refused.exception.status, -10, "XMIP_E_MALFORMED")
        self.assertEqual(self.xmip.unheard(), [])


if __name__ == "__main__":
    unittest.main()
