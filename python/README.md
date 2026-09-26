# Xmip Events from Python

`xmip_event` binds `include/xmip_operate.h` section 11 over `ctypes`
(ADR-0065), with nothing to install beyond Python 3.10 or newer. It crosses
the boundary and nothing more: which Events a filter matches, whether a
subscriber may see them, the queue and the audit are the runtime's.

- `Library(path)` loads the runtime's library and looks up the six
  `XMIP_EVENT_*_ENTRYPOINT` symbols once.
- `subscribe` returns a `Subscription`; `next(timeout_ms, max)` returns a
  `Delivery` of `Event`s, or `None` when nothing arrived. It waits until an
  Event arrives, not on a timer.
- `listen` takes a callable, called with each `Event` on a thread the
  runtime starts, one call at a time.
- `publish(event)` returns how many subscriptions took it. An empty `id` is
  minted and a `time_unix_nanos` of 0 is now.
- Any status other than `XMIP_OK` raises `EventError`, carrying the status
  and, for a refused subscription, the authorization gate's sentence.

## Use

```python
from xmip_event import Filter, Library, Outcome

xmip = Library("xmip_core_runtime.dll")
failures = Filter(outcomes=(Outcome.FAILURE,), scope="xmip:///cluster-a")
with xmip.subscribe("my-program", "/var/log/my-program",
                    "0198a3c4-0000-7000-8000-000000000042", failures) as subscription:
    delivery = subscription.next(timeout_ms=1000)
    for event in delivery.events if delivery else []:
        print(event.type, event.scope)
```

The directory is where the subscription is audited; an empty one lets the
capability decide, which on Windows is the Event Log.

## Test

`test_xmip_event.py` holds the binding's numbers and entrypoint names to the
header, and runs through the runtime's library when both variables are set.
From this folder, in PowerShell:

```powershell
$env:XMIP_RUNTIME_LIBRARY = '<runtime library>'
$env:XMIP_EVENT_AUDIT_DIRECTORY = '<audit directory>'
python -m unittest -v test_xmip_event
```

Or with the other bindings, from the repository root:

```powershell
./verify-event-bindings.ps1 -Language Python -Library <runtime library>
```
