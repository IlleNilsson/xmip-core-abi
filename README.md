# xmip-core-abi

The Xmip application binary interface (ABI): the stable boundary used by the
runtime, operator surfaces, and loadable Modules.

`include/xmip_module.h`, `include/xmip_operate.h` and `doc/specification.md`
are normative. The Rust crate, whose source sits aside under `.src`
(ADR-0049), is a convenience binding over that boundary and must not introduce
Rust-specific types into the ABI: the descriptor and the two judgements a
load makes of it — `validate_module_abi`, whether it is well formed, and
`accepts`, ADR-0012's compatibility rule against the capability loading it,
which the runtime's loader calls — the manifest, the FFI shapes and the
operate types, section 6's start and validate shapes, section
7's rule exports (`operate::rule`), section 8's publication reader
(`operate::publication`), section 9's audit record (`operate::audit`,
with `XMIP_EVENT_SOURCE`, the Windows Event Log source, ADR-0062) and section
10's five exports over the cluster's `xmip.toml` (`operate::design`: its
views, an Xmip Application's routes among them, a filter's structure and
text, an edit, and each node's slice — ADR-0064; ADR-0031, amendment
2026-10-05), which the VS Code extension's language server calls and
`RuntimeRules.Design` binds for the Operation Desktop, and section
12's technology catalogue (`operate::catalogue`: the technologies a runtime
carries and the settings each declares, ADR-0064 amendment 2026-09-26),
which the language server calls and `RuntimeRules.Catalogue` binds, and section
13's System Process declaration (`operate::process`: a process declares itself
and the declarations standing in a directory are read, both `xmip-core-node`'s,
ADR-0053), which `RuntimeRules.Processes` binds. `examples/conforming.rs`
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
and the probe's judgement of whether a library loaded at all
(`ModuleProbe.Result.Unloadable`) and whether a module conforms, and `StatusMeaning`,
what a status code means; `Operate/` the operator boundary and its records,
and `RuntimeRules`, section 7 bound once: scope containment, a scope's
parts and the node and stage it is on, the stage words and the facts of a stage, the role words, a declaration's parse and the stages a role serves, a
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
`AuditSeverity` and `AuditKept` as the header defines them (ADR-0062), and
`Read`, the records read back by the capability's one reader and query —
`AuditRead`, its `AuditEntry` page and its `AuditGroup`s one step down the
drill (ADR-0062, amendment 2026-09-29). Section 11 — Events, subscribed (ADR-0065) — is bound once as
`RuntimeRules.Events` (`RuntimeEvents`): `Subscribe` returns a disposable
`EventSubscription` drained with `Next(timeout, max)`, `Listen` calls a
handler on the runtime's listener thread, and `Publish` hands an Event to
every matching subscription in the process; `AuthorizeBy` hands the hub
the program's policy of who may subscribe, asked of each attempt as an
`EventAuthorization` and answering allow, deny or no opinion — being in the
process admits nobody (ADR-0065, amendment 2026-09-26) — and every `EventDelivery` carries `Unheard`, the members of
the cluster not heard now as `UnheardRecord`s, with `UnheardChanged`, a
drain waking for that alone (amendment 2026-10-02); `Unheard()` asks the
hub; `EventRecord`, `EventFilter`,
`EventAction` and `EventOutcome` are the header's. What an operator lists and
does of Event subscriptions is `RuntimeRules.EventSubscriptions`
(`RuntimeEventSubscriptions`): `Standing`, the hub's `EventSubscriptionList`
of `EventSubscriptionRecord`s, and `Act`, pause, resume or remove one; a
`Publication` carries its own `EventSubscriptions`, each list with the
members its nodes do not hear (`EventSubscriptionList.Unheard`) (ADR-0065, amendments
2026-09-29 and 2026-09-30). Section 14 — a node's Subscriptions (ADR-0013,
amendment 2026-09-30) — is `RuntimeRules.Subscriptions`
(`RuntimeSubscriptions`): `Standing`, the `SubscriptionList` of
`SubscriptionRecord`s of every node running in the process, `Act`, pause or
resume one — there is no remove — and `Order`, an act on any noun left
where a publication says; a `Publication` carries its own `Subscriptions`.
Section 15 — a node's Dead Message Queue (ADR-0052, amendment 2026-10-01) —
is `RuntimeRules.DeadMessages` (`RuntimeDeadMessages`): `Standing`, the
`DeadMessageList` of `DeadMessageRecord`s every node running in the process
keeps, each with its gate verdicts, promoted properties and declines as name
and value pairs, and `Replay`, one by its node and Message; a `Publication`
carries its own `DeadMessages`, and a Replay over one is `Order` on the noun
`dead-message`. Section 16 — a Journey that failed (runtime-model.md section
13; ADR-0013) — is `RuntimeRules.Journeys` (`RuntimeJourneys`): `Act`, retry
or dismiss one by its node and identifier, by who acts; over a publication
it is `Order` on the noun `journey` — and `Failed`, the `FailedJourneyList`
of every Journey that failed at the Send Ports of the nodes running in the
process, each Port's `FailedJourneyPort` with its count, the place its next
page reads from and its `FailedJourneyRecord`s, read from Xmip Storage a page
at a time; a `Publication` carries its own `FailedJourneys`, each Port's
count and oldest hundred. `Xmip.Surface` lists them through
`IOperatorSurface.FailedJourneys` at a cluster, a node or one Send Port's
scope (`JourneyOperation.PortAt`, `JourneyOperation.Within`). A Port's page
may be empty with a next place, where the stretch the node read held no
failed Journey: the place, not the Journeys, says whether more remain. A
list is an answer (`FailedJourneyList.Listed`) even when it lists none;
`FailedJourneyList.Unlisted` is a surface that cannot list them — no node
reached, no runtime loaded, no publication read yet — and
`FailedJourneyList.Unanswered` one that asked and was not answered, its
`Failure` the sentence, opening `FAILED:`, in the runtime's words: where Xmip
Storage does not answer, `NativeOperator` says so through
`JourneyOperation.Read` rather than throwing. The evidence a Send
Port publishes is read in one place each: the last Journey that ever failed
there by `JourneyOperation.FailedIn`, how many wait in its queue now by
`JourneyOperation.FailingIn`.
Section 10 — the
cluster's `xmip.toml` read, edited and sliced — is `RuntimeRules.Design`
(`RuntimeDesign`): `TryViews`, `TryEdit`, `TrySlices`, `TryFilterStructure`
and `TryFilterText`, each the header's JSON or text, or the runtime's
refusal; `Xmip.Surface` reads them as `ConfigurationViews`, `ClusterEdit` and
`NodeSlice`, which the desktop's Configure page draws and sends. Section 12 — the
technologies the runtime carries and what each declares a Location may set —
is `RuntimeRules.Catalogue` (`RuntimeCatalogue`): `TryRead` brings back the
header's JSON, every technology or the one named, for the desktop editor's
Location form. Section 13 — a System Process declared (ADR-0053) — is
`RuntimeRules.Processes` (`RuntimeProcesses`): `Declare` has the node write
the calling process's declaration and answers the file, or the node's
refusal of a purpose that is no word; `Read` brings back the declarations
standing in a directory as `ProcessDeclarations` of `ProcessStanding`. In a
composed
estate the project copies the runtime's built library beside everything that
references it. `AbiBoundaries` says both boundaries at once. `xmip-cli abi`, `status` and
`probe` render those and `Get-XmipAbi`, `ConvertFrom-XmipStatus` and
`Get-XmipModuleDescriptor` emit them, so neither face judges a code or a
module on its own (ADR-0052, amendment 2026-09-24). `dotnet/Xmip.Abi.Test`
compares both against the headers and holds those answers; `dotnet test dotnet/Xmip.Abi.Test` runs
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
  (ADR-0052, amendment 2026-09-20). `NativeOperator.Plan` hands a node's
  file to `xmip_start_v1`, which reads, validates and publishes its plan and
  runs nothing; its sentence says *planned … not running*. A host that
  `RemoteOperator` loses is told at once to every watch, with a `Source` that
  says it is unreachable and why, and every answer is empty until it is back
  — never the publication held from before; while anything watches, the host
  is tried again on `RemoteOperator.Retry` (SignalR's 0, 2, 10, 30 seconds,
  the last repeated), whether it was lost or never reached, and every watch
  is told the moment it answers (`RemoteOperatorTest`). A snapshot is read once per change: a
  followed `SnapshotOperator` reads each publication before its change feed
  announces it, and answers every question from that reading without touching
  the file; `SurfaceChoice.OpenAll` follows every snapshot it opens
  (`ClusterSurfaces.Follow`), so no render reads one (ADR-0052, amendment
  2026-10-03; `SnapshotReadCostTest` holds the bound). A watch begun before
  its source recovers when the source arrives (`PathArrival`): a
  `SnapshotOperator` whose publication's directory is not made yet, and a
  `NativeOperator` whose runtime library is not built yet or would not load
  (`RuntimeLoad`, which tries a refused file again once it is written
  again), each say their first view, wait for the file system's notice and
  announce a change the moment the source is there — a retry, its interval
  doubling to five seconds, only where not even the path's root exists to
  watch (`SurfaceArrivalTest`).
- **Subscriptions.** `IOperatorSurface.Subscriptions` lists what the nodes
  route by and `IOperatorSurface.Act` pauses or resumes one (`SubscriptionAct`,
  which holds no remove, answered as a `SubscriptionOperation`; a
  Subscription is added and removed in the TOML configuration,
  `SubscriptionOperation.Configured` says so in words): in the node's
  process for `NativeOperator`, through the host's hub for `RemoteOperator`,
  and for `SnapshotOperator` left where the publication says its publisher
  takes orders, or declined where it says nowhere. `SubscriptionQuery` is the
  one drill, pattern and order every surface asks of them (ADR-0013,
  amendment 2026-09-30).
- **Event subscriptions.** `IOperatorSurface.EventSubscriptions` lists what
  the nodes' hubs hold and `IOperatorSurface.Act` pauses, resumes or removes
  one (`EventSubscriptionAct`, answered as an `EventSubscriptionOperation`),
  reached the same three ways. `EventSubscriptionQuery` is their one drill,
  pattern and order (ADR-0065, amendments 2026-09-29 and 2026-09-30), and
  `EventSubscriptionQuery.Unheard` and `Line` the one selection and wording
  of the members the nodes there do not hear — *R1: not hearing
  `<node>` since `<time>`: `<why>`* — which every surface shows read-only
  beside them (amendment 2026-10-02); the links between nodes are never
  listed or acted on.
- **The tree.** `ScopeTree` — the tree, its rollup and the worst leaf beneath
  a scope, over the runtime's containment, parts, stage words and order;
  `ScopeIndex`, a publication read once as that tree with every answer a
  lookup (ADR-0052, amendment 2026-09-15); `Branch` and `Crumb` for
  the drill-down, and `ScopeItem` for one row of it — its mood, its figures
  and the leaf that explains it (`Worst`), the next scope on the way to the
  cause — whether it `Exists`, and `Selected`, the rows a selection names,
  worst first under a wildcard, which `xmip-cli show` and `Get-XmipScope`
  both answer with. Every drill starts at `IOperatorSurface.Root`, the scope the
  publisher publishes at (a Playground cluster, never the root above it); a
  stage card counts `IOperatorSurface.Stage` and lists `Locations`, and the
  prompt's letters are `MessagePath`, each stage its own figure (the owner,
  2026-09-26: *drill-down does not work and datapoints are wrong*).
- **Narrowing.** `ScopePattern`, the one wildcard every surface matches with
  (ADR-0059 clauses 7 and 8), `observe::wildcard`'s since 2026-09-29, called
  through `RuntimeRules.Matches` (`xmip_scope_matches_v1`); `ScopeFilter`, that pattern applied to a
  publication so the views narrow alike and none decides for itself what a
  pattern means; and `ScopeSelection`, what a scope argument selects on the
  command line and in a cmdlet — the scope itself, or the topmost scopes a
  wildcard names — with the REFUSED sentence when it names nothing.
- **Figures.** `Figures`, the six at a scope in the order every surface says
  them, each also by the kind counted (`Of`); `FigureFlow`, the five as a rate
  per second between two publications; and `FigureWatch`, the one place a
  face remembers figures between two reads — the prompt's letters and the
  board's stage cards alike — so the rate is computed once.
- **Following.** `SurfaceFollow`, the one follow: the answer now, then a new
  one whenever the publication advances and the answer changed, a wildcard
  matched again at every notice, until cancelled — `xmip-cli --follow` and
  `Get-XmipHealth -Follow` / `Get-XmipScope -Follow` run it.
- **What a run says.** `RunHeader` for the `[run]` table a publisher writes —
  `Hidden` where the run declared itself hidden, and `ClusterSurfaces` lists,
  reaches and names such a cluster only for a face that includes what is
  hidden, by the one rule `observe::run::shown` through `RuntimeRules.Shown`
  (`xmip_run_shown_v1`; ADR-0028 and ADR-0052, amendments 2026-09-30) —
  `NodeCapability` for what one node declared — its roles, and the stages they
  serve — never inferred from what the node is called (ADR-0056 clause 1),
  read by the rule of `node::NodeRole::declared` (lowercase exactly, any
  other word refused and the refusal carried in `Refusal`; amendment
  2026-10-01) — and `Topology` for the communication view.
- **Acts and verdicts.** `ScopeAction`, the two acts `xmip_operate.h` carries
  and no start, stop or restart; `ScopeOperation`, the one shape they answer
  in, so an exit code and a pipeline object agree, and `ScopeOperation.Who`,
  who paused — the name stated, else the user the process runs as; and
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
  the runtime rule; a line whose remote is no web host is refused in
  `RemoteOperator.Refusal`'s one sentence, `SurfaceLine.Refusal`),
  `TomlDocument` (the one TOML reader, and the syntax-tree editing an editor
  changes a document through, comments and layout kept), `ProcessDeclaration` (what a System
  Process Xmip owns says of itself, ADR-0053 clause 3, declared and listed
  through the node — section 13 — and never written here), `English` (a status
  said in words once, a mood's word and color name asked of the runtime,
  a stage's name, a rate on its one K, M and G ladder — `Moving` bare for the
  prompt, `Rate` with its unit, `Flow` for a rate not known yet —
  and what a surface says of a scope it holds nothing at, beneath or measured)
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
  name, the directory its configuration names (`Xmip:AuditDirectory`), the
  runtime library the program was told to load (`Library`, so `--runtime`
  reaches the audit), `Record`, `Failed` — one exception, one record — `WatchUnhandled`,
  and `Properties`, the one way a value becomes a record's text for every
  .NET program and the estate's script module — each
  a call into `xmip_audit_v1` and never a record of its own; and
  `Read(AuditQuery)`, every .NET surface's read of the records back — the
  Audit view, `xmip-cli audit` and `Get-XmipAudit` — through
  `xmip_audit_read_v1` (ADR-0062, amendment 2026-09-29); and
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
takes no caller over TLS without one (ADR-0063 clause 1). Every act the hub
serves goes through `GatedOperator` in `Xmip.Surface`, the one role check:
taken only where the host's `RoleContext` may operate, as the subject of the
client certificate the connection proved, else on loopback as the operating
system user the host runs as (`GatedOperator.Proven`) — no hub method takes a
name, and from elsewhere with no certificate no act is taken — and refused in
words otherwise; taken or refused, audited through the host's `ProgramAudit`
(ADR-0009, amendment 2026-10-03). `Role`, `Roles` and `RoleContext` are
`Xmip.Surface`'s, beneath both GUIs and the relay.

`dotnet/Xmip.Surface.Test` covers the tree and its index, the pattern, the
filter and the selection, the figures and their flow, runtime discovery and
the surface precedence, the English, a program's audit — into a directory,
to the operating system's log when the sink fails, and one entry of its own
when audit cannot be reached — the
configuration verdict, the process declaration, the snapshot surface and the
cluster set over fixtures, the remote surface against a hub on a loopback
port — plain, and over mutual TLS with certificates a test authority issued,
where a client certificate the host does not trust, a host certificate the
surface does not trust and no certificate at all are each refused — the
hub's role gate, where an Observer host refuses every act, an Operator host
takes each as the certificate's subject or, on loopback without one, as the
host's own user, and an act from elsewhere without one is refused, over
the test cluster's names from `test/xmip.toml` (`TestCluster`) — and where a
host may bind. It tests no rule the runtime owns: it proves the surface returns what
the runtime's export returns, and those rules are tested once, where they are
written. `dotnet test dotnet/Xmip.Surface.Test` runs them, after `cargo build`
in xmip-core-runtime has left the library the test assemblies load.

`architecture.toml` carries the maturity; this file does not repeat it.
