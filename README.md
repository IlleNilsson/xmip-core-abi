# xmip-core-abi

The Xmip application binary interface (ABI): the stable boundary used by the
runtime, operator surfaces, and loadable Modules.

`include/xmip_module.h`, `include/xmip_operate.h` and `doc/specification.md`
are normative. The Rust crate, whose source sits aside under `.src`
(ADR-0049), is a convenience binding over that boundary and must not introduce
Rust-specific types into the ABI: the descriptor, the manifest, the FFI
shapes and the operate types, section 6's start and validate shapes, section
7's rule exports (`operate::rule`), section 8's publication reader
(`operate::publication`), section 9's audit record (`operate::audit`,
with `XMIP_EVENT_SOURCE`, the Windows Event Log source, ADR-0062) and section
10's four designer exports (`operate::design`: an Xmip Application's routes,
a filter's structure and text, an edit — ADR-0064), which the VS Code
extension's language server calls and no .NET surface binds yet, and section
12's technology catalogue (`operate::catalogue`: the technologies a runtime
carries and the settings each declares, ADR-0064 amendment 2026-09-26),
which the language server calls and `RuntimeRules.Catalogue` binds. `examples/conforming.rs`
builds as a cdylib and is the conforming artifact a loader probe is tested
against.

Section 11's Events (ADR-0065) are `operate::event` in the Rust crate and are
bound here for every language that subscribes in process: `c/` shows
`xmip_operate.h` section 11 from C, with a loader header, an example and a
test; `include/xmip_event.hpp` is a header-only C++17 wrapper whose
subscriptions and batches release themselves; `java/` is the `se.xmip.event`
package over the foreign function and memory API (Java 21 with
`--enable-preview`); `python/xmip_event` binds it over ctypes; and .NET's is
`Xmip.Abi`'s `RuntimeEvents`, below. Each binding only crosses the boundary —
the matching, the authorization, the queue and the audit are
`xmip-core-event`'s, forwarded by the runtime's library. Each has a test that
publishes an Event through `xmip_event_publish_v1`, receives it by `next` and
by callback, and holds publish to receive to a millisecond beside a plain
thread wake; the Java and Python tests also hold their copies of the header's
numbers and entrypoint names to the header.
`./verify-event-bindings.ps1 -Library <runtime library>` builds and runs the C,
C++, Java and Python tests with zig, the JDK and Python, into the git-ignored
`build/`.

The .NET binding is here too: `dotnet/Xmip.Abi`, one class library declaring
both headers for every operator surface — the cli, the PowerShell module and
the GUI reference it as a project and bind nothing themselves (ADR-0014,
amendment of 2026-08-26). `Module/` carries the module boundary, its probe
and the probe's judgement of whether a module conforms, and `StatusMeaning`,
what a status code means; `Operate/` the operator boundary and its records,
and `RuntimeRules`, section 7 bound once: scope containment, a scope's
parts and the node and stage it is on, the stage words, a declaration's parse and the facts of a stage, a
node's published capability and a run's entry for it, a mood's word, color
name and rollup, a counted kind's word and the worst-first order, each written
once in the crate that owns it (`observe`, `node`) and forwarded by the
runtime's library, so no .NET surface writes a rule again; and
`PublicationReader`, section 8, which hands a publication's text to the
runtime's one reader and brings back a `Publication` — its records, counts,
topology (`Topology.cs`, the header's values) and run (ADR-0052, amendments
2026-09-24), and what each topology kind, origin and pattern is called
(`Words`, a `TopologyWord` of the word a publication writes and the name a
person reads, `observe::topology`'s; ADR-0052, amendment 2026-09-25); and `RuntimeAudit`, section 9, a program's audit record handed
to `xmip-core-audit` through the runtime's library, with `AuditPhase`,
`AuditSeverity` and `AuditKept` as the header defines them (ADR-0062). Section 11 — Events, subscribed (ADR-0065) — is bound once as
`RuntimeRules.Events` (`RuntimeEvents`): `Subscribe` returns a disposable
`EventSubscription` drained with `Next(timeout, max)`, `Listen` calls a
handler on the runtime's listener thread, and `Publish` hands an Event to
every matching subscription in the process; `EventRecord`, `EventFilter`,
`EventAction` and `EventOutcome` are the header's. Section 12 — the
technologies the runtime carries and what each declares a Location may set —
is `RuntimeRules.Catalogue` (`RuntimeCatalogue`): `TryRead` brings back the
header's JSON, every technology or the one named, for the desktop editor's
Location form. In a
composed
estate the project copies the runtime's built library beside everything that
references it. `AbiBoundaries` says both boundaries at once. `xmip-cli abi`, `status` and
`probe` render those and `Get-XmipAbi`, `ConvertFrom-XmipStatus` and
`Get-XmipModuleDescriptor` emit them, so neither face judges a code or a
module on its own (ADR-0052, amendment 2026-09-24). `dotnet/Xmip.Abi.Tests`
compares both against the headers and holds those answers; `dotnet test dotnet/Xmip.Abi.Tests` runs
them.

## What every .NET surface shares

`dotnet/Xmip.Surface` (ADR-0052) is the one implementation behind all four
operator surfaces; the GUI hosts, the cli and the PowerShell module are thin
faces over it.

- **Surfaces.** `IOperatorSurface` with its three implementations —
  `NativeOperator` over the binding, `SnapshotOperator` over a published
  snapshot and `RemoteOperator` over a web host's surface hub on another
  machine — and `ClusterSurfaces`, the set a face holds when more than one
  cluster is published: one surface per cluster, and nothing added across them
  (ADR-0052, amendment 2026-09-20).
- **The tree.** `ScopeTree` — the tree, its rollup and the worst leaf beneath
  a scope, over the runtime's containment, parts, stage words and order;
  `ScopeIndex`, a publication read once as that tree with every answer a
  lookup (ADR-0052, amendment 2026-09-15); `Branch` and `Crumb` for
  the drill-down, and `ScopeItem` for one row of it — its mood, its figures
  and the leaf that explains it (`Worst`), the next scope on the way to the
  cause. Every drill starts at `IOperatorSurface.Root`, the scope the
  publisher publishes at (a Playground cluster, never the root above it); a
  stage card counts `IOperatorSurface.Stage` and lists `Locations`, and the
  prompt's letters are `MessagePath`, each stage its own figure (the owner,
  2026-09-26: *drill-down does not work and datapoints are wrong*).
- **Narrowing.** `ScopePattern`, the one wildcard every surface matches with
  (ADR-0059 clauses 7 and 8); `ScopeFilter`, that pattern applied to a
  publication so the views narrow alike and none decides for itself what a
  pattern means; and `ScopeSelection`, what a scope argument selects on the
  command line and in a cmdlet — the scope itself, or the topmost scopes a
  wildcard names — with the REFUSED sentence when it names nothing.
- **Figures.** `Figures`, the six at a scope in the order every surface says
  them, and `FigureFlow`, the three stage figures as a rate per second.
- **What a run says.** `RunHeader` for the `[run]` table a publisher writes,
  `NodeCapability` for what one node declared it can do — never inferred from
  what the node is called (ADR-0056 clause 1), read by the rule of
  `node::Stage::declared` (lowercase exactly, any other word refused and the
  refusal carried in `Refusal`) — and `Topology` for the communication view.
- **Acts and verdicts.** `ScopeAction`, the two acts `xmip_operate.h` carries
  and no start, stop or restart; `ScopeOperation`, the one shape they answer
  in, so an exit code and a pipeline object agree; and
  `ConfigurationVerdict` for what came of handing the runtime a node
  configuration — a saved file, or the text an editor holds.
- **Plumbing.** `RuntimeLibrary` (discovery by one rule, written here and
  nowhere else in the estate: `Xmip:RuntimeLibrary`, else
  `XMIP_RUNTIME_LIBRARY`, else beside the executable, with a library an
  operator typed over all three; and `Rules`, the one library a process calls
  the runtime's rules in — a snapshot or remote surface as much as a native
  one), `SurfaceChoice` (the surface a host chose in
  its TOML, every snapshot it names, and — over a `SurfaceLine` — the one
  precedence the executable, the prompt and the cmdlets share: a remote host,
  a snapshot or a runtime stated for the invocation, then the document, then
  the runtime rule),
  `TomlDocument` (the one TOML reader, and the syntax-tree editing an editor
  changes a document through, comments and layout kept), `ProcessDeclaration` (what a System
  Process Xmip owns says of itself, ADR-0053 clause 3), `English` (a status
  said in words once, a mood's word and color name asked of the runtime)
  and `SurfaceChange` (one coalescing change stream that wakes every
  surface when a published snapshot advances).
- **TLS.** `SurfaceTls`, what a surface presents and trusts when it
  crosses a network (ADR-0063 clause 1): a PEM chain and key and the anchors
  a peer's chain must reach — `Xmip:Certificate`, `Xmip:PrivateKey` and
  `Xmip:TrustAnchor`, else `XMIP_CERTIFICATE`, `XMIP_PRIVATE_KEY` and
  `XMIP_TRUST_ANCHOR`, else nothing presented and the operating system's
  trust store — the check of a peer's certificate for server or client use,
  and `Permits`, the one rule for connecting and binding alike: HTTPS always,
  plain HTTP to this machine only. `RemoteOperator` presents and checks
  through it, and says a refused certificate in its reason.
- **Events.** `EventFeed`, what a .NET face subscribes through: it finds the
  runtime by `RuntimeLibrary`'s one rule, subscribes as a Party's UUID,
  audits where the program's `ProgramAudit` records go, and `Follow` yields
  each Event as it arrives until cancelled. The hub is the process's own, so
  a face hears Events published in the process that loaded the runtime (a
  node it started, or its own `Publish`); a node elsewhere sends its Events
  over the wire (`xmip-core-event`'s `wire`).
- **Audit.** `ProgramAudit`, how every .NET program audits (ADR-0062): its
  name, the directory its configuration names (`Xmip:AuditDirectory`),
  `Record`, `Failed` — one exception, one record — and `WatchUnhandled`, each
  a call into `xmip_audit_v1` and never a record of its own; and
  `OperatingSystemLog`, the one .NET writer to the Windows Event Log or the
  local syslog, used only when the runtime's library cannot be loaded at all
  and saying why.

`dotnet/Xmip.Surface.Relay` is the served half — `SurfaceHub`, answering what
the host's surface answers, and `SurfaceRelay`, pushing the host's change feed
to every remote surface, so a surface is told and never asks (ADR-0052,
amendment 2026-09-15); only a web host references it. `SurfaceBinding` is
where such a host listens: plain HTTP beyond loopback and HTTPS with no
certificate refused before anything listens, the plain loopback addresses
answered for the host to say it binds them, and `UseXmipTls`, every HTTPS
address presenting the host's certificate and checking a caller's; the hub
takes no caller over TLS without one (ADR-0063 clause 1).

`dotnet/Xmip.Surface.Test` covers the tree and its index, the pattern, the
filter and the selection, the figures and their flow, runtime discovery and
the surface precedence, the English, a program's audit — into a directory,
to the operating system's log when the sink fails, and one entry of its own
when audit cannot be reached — the
configuration verdict, the process declaration, the snapshot surface and the
cluster set over fixtures, the remote surface against a hub on a loopback
port — plain, and over mutual TLS with certificates a test authority issued,
where a client certificate the host does not trust, a host certificate the
surface does not trust and no certificate at all are each refused — and
where a host may bind. It tests no rule the runtime owns: it proves the surface returns what
the runtime's export returns, and those rules are tested once, where they are
written. `dotnet test dotnet/Xmip.Surface.Test` runs them, after `cargo build`
in xmip-core-runtime has left the library the test assemblies load.

`architecture.toml` carries the maturity; this file does not repeat it.
