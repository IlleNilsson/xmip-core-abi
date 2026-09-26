# Xmip Events from Java

`se.xmip.event` binds `include/xmip_operate.h` section 11 over the Java
foreign function and memory API (ADR-0065). It crosses the boundary and
nothing more: which Events a filter matches, whether a subscriber may see
them, the queue and the audit are the runtime's.

| Type | What it is |
| --- | --- |
| `Library` | Loads the runtime's library by path and looks up the six entrypoints once; `subscribe`, `listen`, `publish`. |
| `Subscription` | `next(timeout, max)` drains; `close()` unsubscribes. |
| `Event`, `Filter`, `Delivery` | The header's `XmipEvent`, `XmipEventFilter` and one drain, copied into Java. |
| `Action`, `Outcome` | `XmipAction` and `XmipOutcome` with the header's numbers. |
| `EventException` | Any status other than `XMIP_OK`, with the gate's sentence when a subscription is refused. |

## Java 21, preview

The foreign function and memory API is final in Java 22 and a preview API in
Java 21, the JDK the estate installs. The binding compiles with
`--release 21 --enable-preview` and runs with `--enable-preview`, and it uses
only the API that is the same in 21's preview and 22's final release:
`Arena`, `Linker.nativeLinker`, `SymbolLookup.libraryLookup(Path, Arena)`,
`MemorySegment.reinterpret`, `get` and `set` with a `ValueLayout`,
`MemorySegment.copy` with byte arrays, and `allocate(size, alignment)`. It
avoids the string helpers, whose names changed between the two. Classes
compiled with `--enable-preview` on 21 run only on 21; on 22 or newer the same
sources compile without the flag. The binding supports 64-bit platforms,
where `size_t` is a Java `long`.

## Use

```java
try (Library xmip = Library.load(Path.of("xmip_core_runtime.dll"));
     Subscription subscription = xmip.subscribe("my-program", "/var/log/my-program",
             "0198a3c4-0000-7000-8000-000000000042",
             new Filter(List.of(), List.of(Outcome.FAILURE), "xmip:///cluster-a", ""), 0)) {
    subscription.next(Duration.ofSeconds(1), 64)
            .ifPresent(delivery -> delivery.events().forEach(System.out::println));
}
```

`listen` takes a `Consumer<Event>` instead, called through an upcall on a
thread the runtime starts, one call at a time. `next` blocks until an Event
arrives or the timeout passes; close a subscription only after its draining
thread has returned. The directory is where the subscription is audited; an
empty one lets the capability decide, which on Windows is the Event Log.

## Build and run the test

From the repository root, without a build tool (the test is a `main` that
exits non-zero on a failure):

```sh
javac --release 21 --enable-preview -d build/java $(find java -name '*.java')
java --enable-preview --enable-native-access=ALL-UNNAMED -cp build/java \
    se.xmip.event.EventBindingTest <runtime library> <audit directory> include
```

The test also holds `Action`, `Outcome` and the entrypoint names to the
header. Or with the other bindings:

```powershell
./verify-event-bindings.ps1 -Language Java -Library <runtime library>
```
