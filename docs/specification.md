# The Xmip module ABI

**Status:** Accepted.
**ABI version:** 1
**Header:** [`include/xmip_module.h`](../../include/xmip_module.h)
**Decided by:** ADR-0012 (the module boundary), ADR-0011 (naming), ADR-0010 (capability boundaries)

ADR-0012 decided the shape of the boundary and deliberately left the content
open. This document is that content: the rules a module must obey, and the
function tables it must fill. The header is normative. Where this document and
the header disagree, the header wins and this document is a bug.

Scope is the universal boundary plus the four traits in creation wave one —
transport, message, path and contract. The remaining thirteen traits are not
specified here. ADR-0012's reasoning still holds: a trait table designed
without an implementation in front of it is a guess, and guesses become
`v1` and then become permanent.

---

## 1. What a module is

A module is a shared library — `.so`, `.dll` or `.dylib` — that exports exactly
one symbol and answers through function tables.

It is not a Rust crate. Nothing in the boundary is Rust, and nothing in the
boundary may become Rust. A module written in C, Zig, Go or Rust is the same
module to the host, and the host cannot tell which it loaded. That is the
point. Xmip is written in Rust; Xmip's boundary is not.

A module is the unit of loading and runtime upgrade. A repository is the unit
of source, build and release. One repository may ship several modules.

---

## 2. Versioning

Three versions travel in the descriptor, and they answer three different
questions.

| field | question | who compares it |
|---|---|---|
| `abi_version` | do we speak the same protocol at all | the loader, first |
| `trait_major` / `trait_minor` | is this table the shape I expect | the core module owning the trait |
| `module_major/minor/patch` | which build of this module is it | operators and audit, never the loader |

**ABI version** is the whole boundary — the primitives, the descriptor, the
handshake, the vtable header. It changes only when one of those changes, which
should be close to never. A mismatch is a refusal to load. The entrypoint name
carries it (`xmip_create_module_v1`) so two ABI generations can coexist in one
process during a migration.

**Trait version** is per trait, and each trait moves on its own clock. The
transport trait gaining a function does not renumber the path trait. Rules:

- Same `trait_major` is compatible. The host may call any function the module
  declares, and a module may be newer in `trait_minor` than the host.
- A minor bump may only append. Fields are added at the end of the table, never
  inserted, never reordered, never repurposed.
- A major bump may do anything. It is a different trait for compatibility
  purposes and the host will not load it against the old one.
- A module built against a lower `trait_minor` than the host's is loadable. The
  host must not call past the end of what the module declared. This is the only
  case where the host reads `trait_minor` to decide behaviour rather than to
  accept or reject.

**Module version** carries no compatibility meaning to the host at all. It
exists so an operator can say which build is running and so audit can record it.

---

## 3. Loading

1. The host resolves `xmip_create_module_v1` in the library. Absent: reject.
2. The host calls it with a `XmipHost` that outlives the module.
3. The module checks `host->abi_version`. If it cannot support it, it returns
   `XMIP_E_UNSUPPORTED` and leaves `*out` untouched. It must fail here, not
   later.
4. The module fills `*out` and returns `XMIP_OK`.
5. The host checks `descriptor.abi_version`, then `descriptor.provider`,
   `module` and `standard` against the artifact that asked for this module,
   then `trait_major` against the core module owning that trait. Any mismatch
   is a rejection and the host calls `destroy`.
6. `configure` with the artifact's TOML fragment. Once.
7. `start`. Trait calls are legal only between `start` and `stop`.
8. `stop`, then `destroy`. `start` may follow `stop` again.

The descriptor's three name parts are the same three parts as the repository
name under ADR-0011. `xmip-saxon-transform-xslt` reports `provider="saxon"`,
`module="transform"`, `standard="xslt"`. A module whose descriptor disagrees
with its own repository name is malformed, and a module whose descriptor
disagrees with the artifact is refused. The name is not decoration; it is how
the host knows what it is holding.

`vtable` is selected by `descriptor.module`, not cast on faith. A module that
says `module="path"` and hands back a transport table has lied about a value
the host read first, and the host will have already rejected it.

---

### Finding the library

Naming is a platform convention, not an Xmip decision, and the **host** applies it — a module
author never writes these names:

| platform | file |
|---|---|
| Linux | `libxmip_core_transport_http.so` |
| macOS | `libxmip_core_transport_http.dylib` |
| Windows | `xmip_core_transport_http.dll` |

The repository name with hyphens replaced by underscores, the platform prefix where the
platform has one, the platform suffix always. `XMIP_MODULE_PREFIX` and
`XMIP_MODULE_SUFFIX` in the header resolve to the right pair at compile time.

**The host loads by absolute path**, resolved under `defaults.submoduleRoot`. It does not
search. `LD_LIBRARY_PATH`, `DYLD_LIBRARY_PATH` and the Windows DLL search order are all
ways for something other than the intended file to be loaded, and a platform that runs other
people's modules cannot afford that. On Windows this means `LoadLibraryExW` with
`LOAD_WITH_ALTERED_SEARCH_PATH` and a full path, never `LoadLibraryA` with a bare name.

**Load privately.** `dlopen` with `RTLD_LOCAL`, never `RTLD_GLOBAL`. Two modules may
legitimately contain the same symbol — two XSLT engines both statically linking a
compression library, say — and a global namespace makes the second one silently bind to the
first one's copy. Symbol collisions between independently published modules are expected,
not a fault.

### Exporting the entrypoint

The symbol needs C linkage and external visibility. Windows exports nothing unless asked;
ELF and Mach-O export everything unless the build hides it. A module should build with
`-fvisibility=hidden` and use the macro to put one symbol back:

```c
XMIP_EXPORT XmipStatus
xmip_create_module_v1(const XmipHost *host, XmipModule *out);
```

A Rust module writes `#[no_mangle] pub extern "C"` and needs no macro.

### Unloading

`destroy` first, then unload. Never the reverse, and never while anything is in flight.

Unloading a library frees its code. Any pointer still held into it — a vtable, a
`XmipBuffer.release` function, a `last_error` string, a thread the module started — becomes
a jump into unmapped memory. The crash appears far from the cause and blames the host.

So before `dlclose` or `FreeLibrary`:

1. every module instance from this library has had `destroy` called,
2. every `XmipBuffer` it produced has been released,
3. no borrowed `XmipStr` from it is still held,
4. any thread it started has been joined — `stop` must not return until they have.

When a host cannot prove all four, **not unloading is the correct answer**. Leaking a
mapping is survivable; unloading a live one is not. Runtime upgrade of a sub-module depends
on getting this right, which is why it is stated here rather than left to the loader.

## 4. Ownership

There is one rule and it has no exceptions:

> **Whoever allocates, releases. No allocator is shared across the boundary.**

The host and the module may be built by different compilers, against different
runtimes, with different allocators. `free()` on a pointer the other side
allocated is undefined behaviour, and it is the single most likely way to
crash a production node at three in the morning.

Consequences:

- Anything passed **in** is borrowed for the duration of that call only. A
  module that needs to keep it copies it. `XmipSlice` and `XmipStr` are always
  borrowed and never freed by the receiver.
- Anything handed **out** that owns memory is an `XmipBuffer`, which carries
  its own `release` and its own `owner`. The receiver calls
  `XMIP_BUFFER_RELEASE`. Nothing else.
- Anything handed out that is *borrowed* says so and states its lifetime. Two
  such cases exist: `last_error` and `XmipDiagnostic`, both valid only until
  the next call on the same instance. A caller that needs them longer copies
  them.
- Opaque handles — `XmipNode`, the `void*` from `compile` and `load` — are
  released by the module that produced them, through that module's `release`.
  They are meaningless to anyone else and must not outlive their producer's
  `stop`.

Where a result set has an unknown size, the caller supplies the buffer and the
module reports the true count. `evaluate` works this way. The module never
allocates on the caller's behalf and the caller re-calls with a larger buffer
if it was short. This costs a second call in the rare case and removes an
ownership question in every case.

---

## 5. Threading

A module instance is **not** required to be thread-safe. The host serialises
calls on one instance unless the trait says otherwise, and no trait says
otherwise in ABI version 1.

Concurrency is achieved by creating more instances, not by locking one. Each
instance is created by its own call to the entrypoint and has its own `state`.

Two exceptions:

- `XmipDeliverySink.deliver` is called *by* the module, on whatever thread the
  module chooses, and possibly on several at once. The host's sink is
  thread-safe. This is the only inbound concurrency in the boundary.
- `XmipHost.cancelled` and `XmipHost.log` are callable from any thread at any
  time between `create` and `destroy`.

`destroy` must not be called while any call on that instance is in flight.

---

## 6. Unwinding

**No exception, panic or unwind may cross the boundary.** Ever.

Unwinding across an FFI boundary is undefined behaviour, not merely
discouraged, and it stays undefined even when both sides happen to be the same
language. A module that unwinds into the host has corrupted a process that
was executing other people's messages.

Every function a module exports catches everything at its own edge and returns
`XMIP_E_PANIC`. In Rust that means `catch_unwind` at the outermost frame of
every entry; in C++ a `catch (...)`. The same applies in reverse: the host
catches at the edge of every function it puts in `XmipHost` and `XmipWriter`.

`XMIP_E_PANIC` is terminal. The instance that produced it is unusable — its
invariants are unknown by definition. The host destroys it, records the event
and does not retry against it.

---

## 7. Errors

`XmipStatus` is a negative integer or `XMIP_OK`. The header groups the codes by
who is at fault, because that determines who gets paged:

- **Caller error** (`-1` to `-9`) — the call was wrong. Repeating it unchanged
  fails again.
- **Data** (`-10` to `-19`) — the input is at fault. Neither the caller nor the
  environment. This is the group that goes to the party who sent the message,
  not to the operator.
- **Environment** (`-20` to `-29`) — something outside the process.
- **Control** (`-30` to `-39`) — not failures.
- **Terminal** (`-40` to `-49`) — the instance is unusable.

The split between `XMIP_E_MALFORMED` and `XMIP_E_CONTRACT` is load-bearing.
Malformed means it is not the standard it claims to be — invalid XML, a broken
X12 envelope. Contract means it parses perfectly and violates the rules —
a missing required element, a value out of range. The first is a sender bug in
their serialiser; the second is a sender bug in their data. Different people
fix them.

**Retryability is a property of the code**, expressed once in
`XMIP_IS_RETRYABLE` and not re-decided per call site. `xmip-core-resilience`
reads it and does not need to know which module it is retrying. Only
`TIMEOUT`, `UNAVAILABLE`, `CAPACITY` and `AGAIN` are retryable. Notably
`XMIP_E_IO` is not: an I/O error is as likely to be a full disk as a blip, and
a module that knows its I/O error is transient returns `UNAVAILABLE` and says
so.

Detail beyond the code comes from `last_error`, which is borrowed and valid
until the next call. It is for humans. Nothing in Xmip may parse it.

Where an operator needs structure — contract validation above all — a trait
returns `XmipDiagnostic` instead: a code, a message, a path expression saying
*where*, and a byte offset. A validator that reports only "invalid" against a
40 MB EDI file has told the operator nothing.

---

## 8. Streams

An Xmip stream may be larger than memory. It never crosses the boundary as a
buffer; it crosses as `XmipReader` or `XmipWriter` — a context pointer and one
or two functions.

This is what makes a transfer-depth journey possible. A module that only moves
bytes never materialises them, and a 4 GB file costs the same memory as a 4 KB
one.

- `read` returns bytes written, `0` at end of stream, or a negative status. **A
  short read is not end of stream.** A caller that treats it as one will
  truncate large messages under load and nowhere else, which is the worst
  possible failure schedule.
- `write` returns bytes accepted or a negative status. A partial write is
  legal; the caller re-offers the remainder.
- `finish` is called exactly once, **including on the failure path**, with the
  outcome so far. A sink must be able to distinguish a stream that completed
  from one that was abandoned — an archive module that cannot tell will commit
  half a message.

---

## 9. Cancellation

Cooperative. `XmipHost.cancelled` returns non-zero and the module unwinds its
own work and returns `XMIP_E_CANCELLED`. There is no forced termination,
because there is no safe way to force-terminate code holding a socket and a
half-written file.

A module doing long work polls it. In a read loop, per iteration is right.
`cancelled` is cheap by contract.

---

## 10. The wave-one traits

### Transport

Direction-neutral, per ADR-0010. One module may declare `XMIP_DIR_RECEIVE`,
`XMIP_DIR_SEND` or both, and the artifact decides which is used. HTTP is the
same protocol whether Xmip is listening or calling, and the previous split into
`xmip-receive-*` and `xmip-send-*` duplicated 44 repositories to say so twice.

Receiving is push: the host installs a `XmipDeliverySink` before `start` and
the module calls `deliver` when a stream arrives, on its own thread. Sending is
pull: the host calls `send`. A module that declares both implements both; a
module that declares one returns `XMIP_E_UNSUPPORTED` from the other.

`deliver` carries a `reply` writer, which is NULL when the transport has no
reply channel. A transport that *has* one supplies it even when the artifact
turns out not to use it — whether a reply is possible is a fact about SFTP
versus HTTP, not a fact about the artifact.

### Message

A content handler. It parses a representation into a tree and writes a tree
back out. It does not address into the tree and it does not judge it.

`probe` exists for content negotiation: given the head of a stream, how
confident is this module that the stream is its representation, 0 to 100. It
must not block and must not allocate — the host may call twenty of them to
decide one message.

The tree interface is four functions and no iterator, because an iterator is
state and state across an FFI boundary is a lifetime question. `child_at` by
index is duller and answers no questions.

### Path

A path module never parses. It receives the *message module's* vtable and a
root, and walks whatever representation that table exposes.

This is the whole reason Contract, Message and Path are three traits and not
one. `xmip-core-path-xpath` over a JSON message is not a special case to be
written; it is what falls out when the path module addresses an abstract tree.
The same holds for JSONPath over XML, and for `xmip-core-path-dot` over
anything at all.

`evaluate` fills up to `cap` results and reports the true count in `out_len`.
A predicate may match more than one node — that was the open question in the
hierarchy note, and this is the answer: the path trait is a node-set trait, and
a single-node result is a set of one.

### Contract

A contract judges a stream against a standard. `descriptor` is whatever
identifies the contract in that standard's own terms — a schema document, a
profile URL, a resource name — and only the module interprets it. Xmip does not
model schemas; it models the act of validating against one.

`implies` is the contract-implication idea from the manifest, made concrete.
It answers what the contract already determines, so an artifact does not
restate it. A FHIR contract implies its message representation. An EDI X12
contract implies its delimiters. `XMIP_E_NOT_FOUND` where the standard implies
nothing about the key. This is what keeps artifact configuration short and what
stops an operator setting a delimiter that the standard already fixed.

---

## 11. Conformance

A module conforms when:

1. It exports `xmip_create_module_v1` and nothing else that Xmip requires.
2. Its descriptor's three name parts match its repository name under ADR-0011.
3. No unwind escapes any exported function.
4. It frees nothing it did not allocate, and releases everything it did.
5. It holds no borrowed pointer past the call it arrived in.
6. It returns a retryable status only for a condition that is actually
   retryable.
7. It tolerates `stop` without `start`, `destroy` without `stop`, and
   `configure` with a TOML fragment containing keys it does not recognise.

Point 7 is not politeness. A host crashing during recovery calls these in
orders that a happy path never produces.

A conformance suite belongs in `xmip-core-module-conformance` and can drive
every one of these from outside the module. It does not exist yet.

---

## 12. Deliberately absent

- **An allocator in `XmipHost`.** Sharing one would let a module hand back
  memory the host frees, which reintroduces the problem section 4 removes.
- **A clock.** A module that needs time asks the operating system. A module
  that needs *journey* time is asking for something the journey model owns.
- **Async.** There is no future, no poll, no waker. Async models do not
  survive an FFI boundary intact, and any attempt to carry one across pins both
  sides to one language's runtime — which is precisely what this boundary
  exists to avoid. Concurrency is instances and threads.
- **Generics of any kind.** They do not exist in C and they cannot be faked
  without inventing a type system in the descriptor.
- **`dyn Trait`, `Box`, `String`, `Vec`, or any Rust type.** Per ADR-0012.
  These are not stable across compiler versions, let alone languages.
- **Thirteen of the seventeen traits.** By design.

## 13. Bindings

`abi` is a core module with a plugin surface, not internal plumbing. It is a **surface
module** under ADR-0011: a provider extends Xmip's own surface rather than implementing an
external specification, so the name takes a provider and stops.

```text
xmip-core-abi     Xmip's Rust binding
xmip-acme-abi     Acme's binding, in whatever language Acme works in
```

Anyone may publish one. A binding is a convenience over this header, never the definition
of the boundary, and never normative. A module that skips every binding and writes
`extern "C"` by hand is exactly as conformant — which is the point of specifying the
boundary in C rather than in a language.

`xmip-core-abi` replaces two crates that exist today and carry three names between them,
none of which parse under ADR-0011:

| directory | package | why it fails |
|---|---|---|
| `crates/xmip-module-abi/` | `xmip-abi` | directory and package disagree |
| `crates/xmip-module-api/` | `xmip-module-api` | there is no provider named `module` |

`xmip-module-api` collapses rather than moves. Its entire content is
`pub use xmip_core::contracts::*` plus a re-export of the other crate. The first of those
has to go — it is what pulls an implementer into Rust, and into AGPL by linkage — and once
it does, nothing is left worth renaming.

This is a source change in live crates with dependents, not a specification question, and
belongs in its own reviewed change.

---

## 14. The licence of the boundary

`include/xmip_module.h` is AGPL-3.0-or-later, like the rest of Xmip. There is no
exception for the boundary.

A permissive header was considered and rejected. The case for it: the header is
the one file a third party must copy into their own build, so under AGPL it
carries AGPL with it. The case against, and the one that decided it: Xmip does
not undertake to resolve anyone's licensing position. A user takes Xmip under
Xmip's licence, and may additionally have to satisfy the licences of what Xmip
itself depends on. Reconciling that is the user's business.

The boundary remains a boundary in the sense that matters to this document. It
is a C ABI, it is language-neutral, and no implementer is obliged to write Rust
or to link Xmip code. What it is not is a licence exemption. "The boundary is
the trait, not the licence" describes where Xmip stops dictating *design* — it
was never a promise about *licence*.

A note for implementers, and not legal advice: whether an AGPL header propagates
to code that includes it is contested, and turns on facts about the header and
on jurisdiction. Anyone intending to ship a module under a different licence
should take their own advice rather than rely on this document.

Recorded as clause 9 of ADR-0012.
