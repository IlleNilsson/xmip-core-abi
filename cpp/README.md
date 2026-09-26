# Xmip Events from C++

`include/xmip_event.hpp` is a header-only C++17 wrapper over
`include/xmip_operate.h` section 11 (ADR-0065). It owns resources and
nothing else: which Events a filter matches, whether a subscriber may see
them, the queue and the audit are the runtime's.

- `xmip::event::Library` loads the runtime's library by path and resolves
  the six `XMIP_EVENT_*_ENTRYPOINT` symbols once. It outlives every
  subscription it makes.
- `Subscription` unsubscribes in its destructor. `next` returns a `Batch`,
  or nothing when the wait timed out.
- `Batch` frees itself in its destructor. Its `EventView`s expose every
  string as a `std::string_view` borrowing from the batch.
- `listen` takes a `std::function<void(const EventView &)>`, called on a
  thread the runtime starts, one call at a time; the view is valid for that
  call only.
- Any status other than `XMIP_OK` throws `xmip::event::Error`, carrying the
  status and, for a refused subscription, the authorization gate's sentence.

## Use

```cpp
#include "xmip_event.hpp"

xmip::event::Library xmip("xmip_core_runtime.dll");
xmip::event::Filter filter;
filter.scope = "xmip:///cluster-a";
filter.outcomes = {XMIP_OUTCOME_FAILURE};

auto subscription = xmip.subscribe("my-program", "/var/log/my-program",
                                   "0198a3c4-0000-7000-8000-000000000042", filter);
if (auto batch = subscription.next(1000)) {
    for (std::size_t i = 0; i < batch->size(); i++) {
        std::string_view type = (*batch)[i].type();
    }
}

auto listening = xmip.listen("my-program", "/var/log/my-program",
                             "0198a3c4-0000-7000-8000-000000000042", filter,
                             [](const xmip::event::EventView &event) { /* ... */ });
```

The directory is where the subscription is audited; an empty one lets the
capability decide, which on Windows is the Event Log.

## Build and run the test

With zig (`prerequisite.toml`, `c`), from the repository root:

```sh
zig c++ -std=c++17 -O2 -I include cpp/xmip_event_test.cpp -o build/cpp/xmip_event_test
build/cpp/xmip_event_test <runtime library> <audit directory>
```

Or with the other bindings:

```powershell
./verify-event-bindings.ps1 -Language Cpp -Library <runtime library>
```
