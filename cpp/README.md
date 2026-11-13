# Xmip Events from C++

`include/xmip_event.hpp` is a header-only C++17 wrapper over
`include/xmip_operate.h` section 11 (ADR-0065). It owns resources and
nothing else: which Events a filter matches, whether a subscriber may see
them, the queue and the audit are the runtime's.

- `xmip::event::Library` loads the runtime's library by path and resolves
  the `XMIP_EVENT_*_ENTRYPOINT` symbols a subscriber calls once. It outlives
  every subscription it makes. `authorize_by` hands the hub the program's policy
  of who may subscribe — being in the process admits nobody — and `unheard` says, as the
  header's JSON, which members of the cluster the process's hub does not
  hear now (ADR-0065, amendments 2026-09-26 and 2026-10-02).
- `EventSubscription` unsubscribes in its destructor. `next` returns a `Batch`,
  or nothing when the wait timed out.
- `Batch` frees itself in its destructor. Its `EventView`s expose every
  string as a `std::string_view` borrowing from the batch, and its
  `unheard()` is who was not heard when it was drained, as the header's
  JSON.
- `listen` takes a `std::function<void(const EventView &)>`, called on a
  thread the runtime starts, one call at a time; the view is valid for that
  call only.
- Any status other than `XMIP_OK` throws `xmip::event::Error`, carrying the
  status and, for a refused subscription, the sentence that refused it.
- No argument names the subscriber: it is the account this process runs
  as, resolved through the Parties of the node serving in the process
  (ADR-0019, amendment 2026-11-11). Where no node serves, or no Party holds
  the account, `subscribe` and `listen` throw `XMIP_E_AUTH`. Only code in a
  node's own process can subscribe, and no export lets a node serve in a
  standalone program's process yet, so the snippet below is refused there
  today. The test runs where no node serves and proves exactly that, beside
  publish; the batch, the `listen` trampoline and their latency are
  untested until a node serves in its process (open problem 66).

## Use

```cpp
#include "xmip_event.hpp"

xmip::event::Library xmip("xmip_core_runtime.dll");
xmip::event::Filter filter;
filter.scope = "xmip:///CL1";
filter.outcomes = {XMIP_OUTCOME_FAILURE};

auto subscription = xmip.subscribe("my-program", "/var/log/my-program", filter);
if (auto batch = subscription.next(1000)) {
    for (std::size_t i = 0; i < batch->size(); i++) {
        std::string_view type = (*batch)[i].type();
    }
}

auto listening = xmip.listen("my-program", "/var/log/my-program", filter,
                             [](const xmip::event::EventView &event) { /* ... */ });
```

The directory is where the subscription is audited; an empty one lets the
capability decide, which on Windows is the Event Log.

## Build and run the test

With the C++ compiler Rust's toolchain already requires (`prerequisite.toml`,
`linker`), from the repository root. On Windows, the MSVC build tools' `cl.exe`
in a Developer PowerShell for Visual Studio:

```powershell
cl /nologo /std:c++17 /EHsc /O2 /I include cpp/xmip_event_test.cpp /Fobuild/cpp/ /Febuild/cpp/xmip_event_test.exe
build/cpp/xmip_event_test.exe <runtime library> <audit directory>
```

On Linux and macOS:

```sh
c++ -std=c++17 -O2 -pthread -I include cpp/xmip_event_test.cpp -o build/cpp/xmip_event_test
build/cpp/xmip_event_test <runtime library> <audit directory>
```

Or with the other bindings:

```powershell
./verify-event-bindings.ps1 -Language Cpp -Library <runtime library>
```
