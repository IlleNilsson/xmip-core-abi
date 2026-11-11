# Xmip Events from C

`include/xmip_operate.h` section 11 is the C binding: a program includes the
header, loads the runtime's library (`xmip_core_runtime.dll`,
`libxmip_core_runtime.so` or `libxmip_core_runtime.dylib`) and looks up the
nine `XMIP_EVENT_*_ENTRYPOINT` symbols a subscriber calls by name
(ADR-0065). Which Events a filter matches, whether a subscriber may see them,
the queue and the audit are the runtime's; nothing here decides any of it.
Being in the process admits a subscriber to nothing (ADR-0065, amendment 2026-09-26): the program hands the hub its policy of who may subscribe, a callback answering allow, deny or no opinion, with `xmip_event_authorize_v1`; no node hands its own as it starts yet (open problem 67). Every drain says who in the cluster is not heard now (`xmip_event_batch_unheard_v1`, JSON; amendment 2026-10-02), and a change wakes a waiting drain with no Event; a listening subscription asks `xmip_event_unheard_v1`.

| File | What it is |
| --- | --- |
| `xmip_event_library.h` | Loads the library by path (`LoadLibraryA` on Windows, `dlopen` elsewhere) and resolves the nine symbols into an `XmipEventLibrary`: `subscribe`, `next`, `batch_free`, `listen`, `unsubscribe`, `publish`, `authorize`, `batch_unheard` and `unheard`. |
| `xmip_event_example.c` | A small program: subscribe, publish, drain, print, free, unsubscribe; where no node serves in its process it prints the refusal's sentence instead. |
| `xmip_event_test.c` | The test: a program subscribing names no Party of its own; where no node serves, subscribe and listen are refused `XMIP_E_AUTH` with the sentence, whatever the policy allows; publish, the policy and who is unheard cross. |

## Use

```c
#include "xmip_event_library.h"

XmipEventLibrary xmip;
xmip_event_library_open("xmip_core_runtime.dll", &xmip);

XmipEventFilter filter = {0};              /* every empty list is any */
filter.scope = xmip_str("xmip:///C1");
XmipEventSubscription *subscription = NULL;
uint8_t said[512];
size_t said_len = 0;
xmip.subscribe(xmip_str("my-program"), xmip_str("/var/log/my-program"), &filter, 0,
               &subscription, said, sizeof said, &said_len);

XmipEventBatch *batch;
const XmipEvent *events;
size_t len;
uint64_t refused;
if (xmip.next(subscription, 1000, 64, &batch, &events, &len, &refused) == XMIP_OK) {
    /* every XmipStr in events[0..len) borrows from batch */
    xmip.batch_free(batch);
}
xmip.unsubscribe(subscription);
```

- No argument names the subscriber: it is the account this process runs as,
  proven by the operating system and resolved through the Parties of the
  node serving in the process (ADR-0019, amendment 2026-11-11). A process
  where no node serves, or an account no Party holds, is refused
  `XMIP_E_AUTH` with the sentence in `said`. Only code in a node's own
  process can subscribe, and no export lets a node serve in a standalone
  program's process yet, so the snippet above is refused there today, and
  draining, call back and their latency are untested from C (open problem
  66). The directory is where the
  subscription, its deliveries and its refusals are audited; an empty
  directory lets the capability decide, which on Windows is the Event Log.
- `next` waits up to the timeout for the first Event and wakes when one
  arrives. `XMIP_E_TIMEOUT` means nothing arrived and there is no batch.
- `listen` takes an `XmipEventCallback` and a context pointer instead; it is
  called on a thread the runtime starts, and the Event is valid for that call
  only.

## Build and run

With the C compiler Rust's toolchain already requires (`prerequisite.toml`,
`linker`), from the repository root. On Windows, the MSVC build tools' `cl.exe`
in a Developer PowerShell for Visual Studio:

```powershell
cl /nologo /std:c11 /O2 /I include /I c c/xmip_event_example.c /Fobuild/c/ /Febuild/c/xmip_event_example.exe
build/c/xmip_event_example.exe <runtime library> <audit directory>
```

On Linux and macOS:

```sh
cc -std=c11 -O2 -pthread -I include -I c c/xmip_event_example.c -o build/c/xmip_event_example
build/c/xmip_event_example <runtime library> <audit directory>
```

The first argument defaults to `XMIP_RUNTIME_LIBRARY`. The test and the other
bindings' tests run from one command:

```powershell
./verify-event-bindings.ps1 -Language C -Library <runtime library>
```
