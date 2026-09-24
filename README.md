# xmip-core-abi

The Xmip application binary interface (ABI): the stable boundary used by the
runtime, operator surfaces, and loadable Modules.

`include/xmip_module.h`, `include/xmip_operate.h` and `doc/specification.md`
are normative. The Rust crate, whose source sits aside under `.src`
(ADR-0049), is a convenience binding over that boundary and must not introduce
Rust-specific types into the ABI: the descriptor, the manifest, the FFI
shapes and the operate types, section 7's rule exports among them
(`operate::rule`). `examples/conforming.rs`
builds as a cdylib and is the conforming artifact a loader probe is tested
against.

The .NET binding is here too: `dotnet/Xmip.Abi`, one class library declaring
both headers for every operator surface — the cli, the PowerShell module and
the GUI reference it as a project and bind nothing themselves (ADR-0014,
amendment of 2026-08-26). `Module/` carries the module boundary, its probe
and the probe's judgement of whether a module conforms, and `StatusMeaning`,
what a status code means; `Operate/` the operator boundary and its records,
and `RuntimeRules`, section 7 bound once: scope containment and a scope's
parts, the stage words and a declaration's parse, a mood's word and color
name and the worst-first order, each written once in the crate that owns it
(`observe`, `node`) and forwarded by the runtime's library, so no .NET
surface writes a rule again (ADR-0052, amendment 2026-09-24). In a composed
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
  the drill-down, and `ScopeItem` for one row of it.
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

`dotnet/Xmip.Surface.Relay` is the served half — `SurfaceHub`, answering what
the host's surface answers, and `SurfaceRelay`, pushing the host's change feed
to every remote surface, so a surface is told and never asks (ADR-0052,
amendment 2026-09-15); only a web host references it.

`dotnet/Xmip.Surface.Test` covers the tree and its index, the pattern, the
filter and the selection, the figures and their flow, runtime discovery and
the surface precedence, the English, the
configuration verdict, the process declaration, the snapshot surface and the
cluster set over fixtures, and the remote surface against a hub on a loopback
port. It tests no rule the runtime owns: it proves the surface returns what
the runtime's export returns, and those rules are tested once, where they are
written. `dotnet test dotnet/Xmip.Surface.Test` runs them, after `cargo build`
in xmip-core-runtime has left the library the test assemblies load.

`architecture.toml` carries the maturity; this file does not repeat it.
