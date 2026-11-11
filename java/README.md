# Xmip Events from Java

`se.xmip.event` binds `include/xmip_operate.h` section 11 over the Java
foreign function and memory API (ADR-0065). It crosses the boundary and
nothing more: which Events a filter matches, whether a subscriber may see
them, the queue and the audit are the runtime's.

| Type | What it is |
| --- | --- |
| `Library` | Loads the runtime's library by path and looks up the entrypoints a subscriber calls once; `subscribe`, `listen`, `publish`; `authorizeBy` hands the hub the program's policy of who may subscribe — being in the process admits nobody (ADR-0065, amendment 2026-09-26) — and `unheard` is who in the cluster the process's hub does not hear now, as the header's JSON. |
| `EventSubscription` | `next(timeout, max)` drains; `close()` unsubscribes. |
| `Event`, `Filter`, `Delivery` | The header's `XmipEvent`, `XmipEventFilter` and one drain, copied into Java; a `Delivery` carries who was not heard when it was drained, as the header's JSON (amendment 2026-10-02). |
| `Action`, `Outcome` | `XmipAction` and `XmipOutcome` with the header's numbers. |
| `EventException` | Any status other than `XMIP_OK`, with the sentence that refused it when a subscription is refused. |

## Java 27

The foreign function and memory API is final since Java 22. The binding
compiles with `--release 27`, the current JDK the estate installs
(ADR-0021), and runs with `--enable-native-access=ALL-UNNAMED`, which grants
the restricted methods it exists to call; javac's `restricted` lint is off
for the same reason. It uses `Arena`, `Linker.nativeLinker`,
`SymbolLookup.libraryLookup(Path, Arena)`, `MemorySegment.reinterpret`, `get`
and `set` with a `ValueLayout`, `MemorySegment.copy` with byte arrays, and
`allocate(size, alignment)`. The binding supports 64-bit platforms, where
`size_t` is a Java `long`.

## Use

```java
try (Library xmip = Library.load(Path.of("xmip_core_runtime.dll"));
     EventSubscription subscription = xmip.subscribe("my-program", "/var/log/my-program",
             new Filter(List.of(), List.of(Outcome.FAILURE), "xmip:///C1", ""), 0)) {
    subscription.next(Duration.ofSeconds(1), 64)
            .ifPresent(delivery -> delivery.events().forEach(System.out::println));
}
```

No argument names the subscriber: it is the account this process runs as,
through the Parties of the node serving in the process (ADR-0019,
amendment 2026-11-11); where no node serves, or no Party holds the account,
`subscribe` and `listen` throw `EventException` with `XMIP_E_AUTH` and the
sentence. Only code in a node's own process can subscribe, and no export
lets a node serve in a Java program's process yet, so the snippet above is
refused there today (open problem 66). `listen` takes a `Consumer<Event>`
instead, called through an upcall on a thread the runtime starts, one call
at a time. `next` blocks until an Event arrives or the timeout passes;
close a subscription only after its draining thread has returned. The
directory is where the subscription is audited; an empty one lets the
capability decide, which on Windows is the Event Log.

## Build and run the test

From the repository root, without a build tool (the test is a `main` that
exits non-zero on a failure):

```sh
javac --release 27 -d build/java $(find java -name '*.java')
java --enable-native-access=ALL-UNNAMED -cp build/java \
    se.xmip.event.EventBindingTest <runtime library> <audit directory> include
```

The test runs where no node serves: it proves subscribe and listen are
refused there with the sentence, and that publish, the policy and who is
unheard cross; it also holds `Action`, `Outcome` and the entrypoint names to
the header. Untested until a node serves in the test's process (open
problem 66): draining, the upcall `Library.called` and its latency. Or with
the other bindings:

```powershell
./verify-event-bindings.ps1 -Language Java -Library <runtime library>
```
