# The Xmip module ABI

**Status:** Accepted.
**ABI version:** 1
**Header:** [`include/xmip_module.h`](../include/xmip_module.h)
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

- Same `trait_major` is compatible when the module's `trait_minor` is less
  than or equal to the host's: a module never asks for more than the host
  offers, and a host refuses a module newer in `trait_minor` than itself
  (ADR-0012, *Compatibility*; the owner, 2026-09-26, settling the two
  readings this section and ADR-0012 once gave).
- A minor bump may only append. Fields are added at the end of the table, never
  inserted, never reordered, never repurposed.
- A major bump may do anything. It is a different trait for compatibility
  purposes and the host will not load it against the old one.
- A module built against a lower `trait_minor` than the host's is loadable. The
  host must not call past the end of what the module declared. This is the only
  case where the host reads `trait_minor` to decide behavior rather than to
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
allocated is undefined behavior, and it is the single most likely way to
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
- Opaque handles — `XmipValue`, the `void*` from `compile` and `load` — are
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

A module instance is **not** required to be thread-safe. The host serializes
calls on one instance unless the trait says otherwise, and no trait says
otherwise in ABI version 1.

Concurrency is achieved by creating more instances, not by locking one. Each
instance is created by its own call to the entrypoint and has its own `state`.

Two exceptions:

- `XmipDeliverySink.deliver` is called *by* the module, on whatever thread the
  module chooses, and possibly on several at once. The host's sink is
  thread-safe. This is the only inbound concurrency in the boundary.
- `XmipHost.canceled` and `XmipHost.log` are callable from any thread at any
  time between `create` and `destroy`.

`destroy` must not be called while any call on that instance is in flight.

---

## 6. Unwinding

**No exception, panic or unwind may cross the boundary.** Ever.

Unwinding across an FFI boundary is undefined behavior, not merely
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
their serializer; the second is a sender bug in their data. Different people
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
bytes never materializes them, and a 4 GB file costs the same memory as a 4 KB
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

Cooperative. `XmipHost.canceled` returns non-zero and the module unwinds its
own work and returns `XMIP_E_CANCELLED`. There is no forced termination,
because there is no safe way to force-terminate code holding a socket and a
half-written file.

A module doing long work polls it. In a read loop, per iteration is right.
`canceled` is cheap by contract.

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
   `configure` with a TOML fragment containing keys it does not recognize.

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

`xmip-core-abi` is Xmip's Rust binding of this header, and anyone may publish
another in whatever language they work in. That `abi` is a surface module any
provider may extend under its own name, and how that name is formed, are
ADR-0012 clause 11 and ADR-0011 in the estate's decision record; that a binding
is a convenience over this header and never normative is ADR-0012 clause 2.
This specification does not restate a decision.

The two earlier crates this binding replaced, `xmip-module-abi` and
`xmip-module-api`, were removed on 2026-08-26; `doc/planning/allocation.toml`
in the estate records why.

---

## 14. The license of the boundary

`include/xmip_module.h` is AGPL-3.0-or-later, like the rest of Xmip, and there
is no exception for the boundary. The reasoning, the rejected permissive
header and the note for implementers are clause 9 of ADR-0012 and ADR-0023 in
the estate's decision record; this specification does not restate a decision.

## 15. The operator boundary

This document specifies the boundary a Module plugs *into*. There is a second
boundary, for the surfaces that drive Xmip *from outside* — the `xmip`
executable, the PowerShell module, the GUI — and it is a separate header:
`include/xmip_operate.h`, decided by ADR-0027.

It shares sections 2, 3 and 8 of this specification — `XmipStr`, the status
codes, the reader and writer pair — by including this header, and defines
nothing above those that this one defines. It versions apart:
`XMIP_OPERATE_VERSION` is not `XMIP_ABI_VERSION`, because a surface gains a
command far more often than a trait gains a method.

Its shape is the opposite of this one's. A Module implements a table Xmip
calls; a surface calls a table Xmip implements. Every call reads a snapshot the
runtime published, and none makes execution wait — the thing that watches must
not be able to stop the thing it watches.

What it carries in version 1: health per scope with its evidence, and
measurements — a scope, what was counted, the value, its window, and when it
was taken. Scope is an Xmip URI over the execution tree. The Rust mirror is
`.src/operate.rs`, and its tests read the header and check every constant.

Section 11 is Events, subscribed from any language (ADR-0065): a program
subscribes with a filter and holds a handle, drains it — a batch whose Events
borrow from it until it is freed — or is called back on a thread the runtime
starts, and unsubscribes; it may publish an Event of its own. The matching,
the authorization of the subscriber and the audit are `xmip-core-event`'s,
forwarded by the runtime's library. Its bindings beside this one — C and C++
over the header, .NET in `dotnet/Xmip.Abi`, Java and Python — decide nothing.
The Rust mirror is `.src/operate/event.rs`. Since 2026-09-29 the section also
carries what an operator lists and does (ADR-0065, amendment of that date):
`xmip_event_subscriptions_v1`, a process hub's Event subscriptions as JSON in
memory; `xmip_event_subscription_act_v1`, pause, resume or remove one by its
number; and `xmip_publication_event_subscriptions_v1`, what a read publication
carries, over section 8's handle. .NET binds the first two as
`RuntimeEventSubscriptions` and the third in `PublicationReader`. The act
left where a publication says is section 14's one order.

Section 12 is the technologies a runtime carries (ADR-0064, amendment
2026-09-26): `xmip_technology_catalogue_v1` answers, as JSON in memory, each
technology's capability, module name and settings — every setting's name,
kind, presence and default, meaning and the side that reads it — as the
technology itself declares them in `xmip-core`'s `settings` shape. A
Location's form is built from it, never written in a surface. The Rust
mirror is `.src/operate/catalogue.rs`; .NET binds it as `RuntimeCatalogue`.

Section 13 is a System Process declared (ADR-0053 clause 3):
`xmip_process_declare_v1` has `xmip-core-node` write the calling process's
declaration — its name, location and purpose (`test` or `runtime`, exact),
and what else it says, key then value — and answers the file, which the
caller removes where its process ends, or the node's refusal of a purpose
that is no word or a key that is not a bare word; and
`xmip_process_declarations_v1` answers, as JSON in memory, the declarations
standing in a directory, the node's own when none is named, whether or not
their processes still run. The file, its directory, its words and its
reading are the node's alone. The Rust mirror is `.src/operate/process.rs`;
.NET binds it as `RuntimeProcesses`.

Section 14 is a node's Subscriptions (ADR-0013, amendment 2026-09-30): a
Subscription picks a published Message up and is configuration, added and
removed in the TOML of the Xmip Application that draws it, so an operator
pauses and resumes one and never removes one. `xmip_subscriptions_v1`
answers, as JSON in memory, the Subscriptions of every node running in the
process — each with its Application, file and entry as the file says it,
its filter and destination, whether it is paused and by whom, what it
picked up and what it holds; `xmip_subscription_act_v1` pauses or resumes
one by its node and name, and refuses remove in words;
`xmip_publication_subscriptions_v1` is what a read publication carries; and
`xmip_order_v1` leaves an act — on a Subscription by its name, or on an
Event subscription by its number — where a publication says its publisher
takes orders, the file `observe::Order`'s alone. The Rust mirror is
`.src/operate/subscription.rs`; .NET binds the first, second and fourth as
`RuntimeSubscriptions` and the third in `PublicationReader`.

Section 15 is a node's Dead Message Queue (ADR-0052, amendment 2026-10-01):
an accepted Message that no Subscription matched is kept in the Ledger with
its entry — its receive context, what its gates concluded, its promoted
properties and every Subscription's reason for declining — and an Operator
replays it once a Subscription is added or fixed. It is not a dead letter
queue: a failed Journey never goes there. `xmip_dead_messages_v1` answers, as
JSON in memory, what the queues of every node running in the process keep,
the oldest hundred of each, each entry's verdicts, properties and declines
as name and value pairs in the order written; `xmip_dead_message_replay_v1`
replays one by its node and Message — routed against the node's
Subscriptions of now, a Journey opened for each match and the entry taken
out, in one write, once; a Message replayed before is said so and not
replayed twice, and one that still matches nothing stays, refused in words;
and `xmip_publication_dead_messages_v1` is what a read publication carries.
A surface over a publication replays through section 14's `xmip_order_v1`,
noun `dead-message`, act `replay`. The Rust mirror is
`.src/operate/dead_message.rs`; .NET binds the first two as
`RuntimeDeadMessages` and the third in `PublicationReader`.

Section 16 is a Journey that failed (runtime-model.md section 13; ADR-0013,
amendment 2026-08-26): a Journey leads to one Send Port, and when every Send
Location of its Port failed its tries it is written Failed, with why, and
waits in its Port's queue for an Operator. A node's publication carries, at
`<node>/send/<Port>`, what the Port sent, what failed and the last Journey
that failed there with why: the identifier an act names. There is no list of
failed Journeys. `xmip_journey_act_v1` applies `retry` or `dismiss`, exact,
to one by its node and identifier, by who acts: Retry writes it Active, its
tries begun anew, and sends it again from the end of its Port's queue — or
from its place, where it blocks a Sequential Send Port; Dismiss writes it
Dismissed, terminal, its history, Message and Stream kept, and takes it out
of the queue. Each is one write under a claim, audited with who acted, as
`journey.retry` or `journey.dismiss`. A node not running here, no such
Journey, one that has not failed, or a node that does not send its Port is
refused in words, opening REFUSED; a word that is no act on a Journey is
invalid. A surface over a publication acts through section 14's
`xmip_order_v1`, noun `journey`, act `retry` or `dismiss`. The Rust mirror is
`.src/operate/journey.rs`; .NET binds it as `RuntimeJourneys`.

Section 9 is a program's audit record (ADR-0062): `xmip_audit_v1` hands one
to `xmip-core-audit`, and since 2026-09-29 `xmip_audit_read_v1` answers, as
JSON in memory, the records an audit query asks for — who (a location and
what is beneath it, a host, a program, one record), the scope pattern,
severity, action and time, sorted by any column and paged — with the groups
one step down the drill and the words a reader offers. The reader and the
query are the capability's alone. The Rust mirror is
`.src/operate/audit.rs`; .NET binds it as `RuntimeAudit`. Section 7 gained
`xmip_scope_matches_v1` the same day, the one wildcard over scopes,
`observe::wildcard`'s.

Section 7 gained `xmip_run_shown_v1` on 2026-09-30, `observe::run::shown`:
whether what a run made is shown, as the run declared (hidden) and the reader
asked (including what is hidden). Section 8's `XmipPublicationHead` gained
`hidden` the same day, 1 where the run declared itself hidden; it takes the
byte `has_topology` was padded beside, so the head's size is unchanged but
`has_topology` moves by one, and an older reader and a newer runtime do not
mix. An audit query takes `hidden` (`include` or `exclude`) and a record and
a group say `hidden` in section 9's JSON (ADR-0028, amendment 2026-09-30).
