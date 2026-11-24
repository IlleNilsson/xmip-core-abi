# xmip-core-abi

The Xmip application binary interface (ABI): the stable boundary used by the
runtime, operator surfaces, and loadable Modules.

`include/xmip_module.h`, `include/xmip_operate.h` and `doc/specification.md`
are normative; xmip-core-operate's library implements `xmip_operate.h` over
the runtime (ADR-0058, amendment 2026-11-16). The Rust crate, whose source sits aside under `.src`
(ADR-0049), is a convenience binding over that boundary and must not introduce
Rust-specific types into the ABI: the descriptor and the two judgments a
load makes of it — `validate_module_abi`, whether it is well formed, and
`accepts`, ADR-0012's compatibility rule against the capability loading it,
which the runtime's loader calls — the manifest, the FFI shapes and the
operate types, section 6's start and validate shapes, section
7's rule exports (`operate::rule`), section 8's publication reader
(`operate::publication`), section 9's audit record (`operate::audit`,
with `XMIP_EVENT_SOURCE`, the Windows Event Log source, ADR-0062) and section
10's five exports over `cluster.xmip` (`operate::design`: its
views, an Xmip Application's routes among them, a filter's structure and
text, an edit, and each node's slice — ADR-0064; ADR-0031, amendment
2026-10-05), which the VS Code extension's language server calls and
`RuntimeRules.Design` binds for the Operation Desktop, and section
12's technology catalogue (`operate::catalogue`: the technologies a runtime
carries and the settings each declares, ADR-0064 amendment 2026-09-26),
which the language server calls and `RuntimeRules.Catalogue` binds, and section
13's System Process declaration (`operate::process`: a process declares itself,
the declarations standing in a directory are read and one is taken away, all
`xmip-core-runtime`'s over `xmip-core-node`'s words, ADR-0053), which
`RuntimeRules.Processes` binds, and section 20's upgrade
of a database's schema (`xmip_database_upgrade_v1`, the scripts
`xmip-core-persist` writes; ADR-0015, amendment 2026-10-20), which
`RuntimeRules.Databases` binds, and section 25's one list of every Report
(`operate::report`: `xmip_report_v1`, its `list` and `open` acts, and
`XmipReportKind`, whether a Report presents data that already exists or
what a Quest probes first; ADR-0072, amendment 2026-11-10), which
`xmip-core-report` keeps and a node's build of xmip-core-operate answers,
and section 26's module library probed (`operate::module_probe`:
`xmip_module_probe_v1`, opened by the runtime's one loader and judged by the
rule a node judges a descriptor by; ADR-0012, amendment 2026-11-17), which
`RuntimeRules.Modules` binds, and section 27's configuration document read,
written and one key set (`operate::document`: `xmip_document_read_v1`,
`_write_v1` and `_set_v1`, `xmip-core-configure`'s, the runtime's one
reader; ADR-0031, amendment 2026-11-17), which `RuntimeRules.Documents`
binds and every .NET surface and the estate's tooling read a configuration
Xmip runs through.
`examples/conforming.rs`
builds as a cdylib and is the conforming artifact a loader probe is tested
against.

Section 11's Events (ADR-0065) are `operate::event` in the Rust crate and are
bound here for every language that subscribes in process: `c/` shows
`xmip_operate.h` section 11 from C, with a loader header, an example and a
test; `include/xmip_event.hpp` is a header-only C++17 wrapper whose
subscriptions and batches release themselves; `java/` is the `se.xmip.event`
package over the foreign function and memory API (Java 27, where it is
final); `python/xmip_event` binds it over ctypes (Python 3.15); and .NET's is
`RuntimeEvents`, in xmip-core-operate's `Xmip.Operate`, over the declarations
`Xmip.Abi` binds, below. Each binding only crosses the boundary —
the matching, the authorization, the queue and the audit are
`xmip-core-event`'s, forwarded by the runtime's library. No argument names
the subscriber: it is the account the process runs as, resolved through the
Parties of the node serving in that process (ADR-0019, amendment
2026-11-11), and a process where no node serves subscribes as nobody. Only
code in a node's own process — a module the node loaded — can subscribe,
and no export lets a node serve in a standalone program's process yet
(`xmip_start_v1` validates and plans and runs nothing), so a C, C++, .NET,
Java or Python program is refused today. Each binding's test, run where no
node serves, proves subscribe and listen refused `XMIP_E_AUTH` with the
sentence, and that an Event published through `xmip_event_publish_v1`, the
policy and who is unheard cross; the Java and Python tests also hold their
copies of the header's numbers and entrypoint names to the header.
Untested until a node serves in a binding's process (open problem 66):
each binding's drain, listen and call back marshalling, and the latency
through it. xmip-core-operate's own tests (`ffi::event`) reach the C
exports, never a binding's marshalling.
`./verify-event-bindings.ps1 -Library <runtime library>` builds and runs the C,
C++, Java and Python tests, into the git-ignored `build/`: C and C++ with the
compiler Rust's toolchain already requires (the MSVC build tools' `cl.exe` on
Windows, `cc` and `c++` elsewhere), Java with the JDK, Python with Python.

The .NET binding is here too: `dotnet/Xmip.Abi`, one class library declaring
both headers for every operator surface — the cli, the PowerShell module and
the GUI reach the C ABI through it and bind nothing themselves (ADR-0014,
amendment of 2026-08-26). It is the binding and no more: each export's
symbol (`OperateAbi`), the structs that mirror the header (`XmipStr`,
`XmipOperate`, `XmipEvent`, `XmipEventFilter`, `XmipHealthEntry`,
`XmipMeasurement`, `XmipPublicationHead`, `XmipTopologyNode`,
`XmipTopologyLink`, `XmipTopologyPlace`), the header's values
(`HealthState`, `Counted`, `RunList`, `Topology.cs`'s kinds, origins,
patterns, relations, bands and columns, `AuditPhase`, `AuditSeverity`,
`AuditKept`, `EventAction`, `EventOutcome`, `RemoteFollows`,
`Protection`, `ReportKind`) and the two ways a .NET string crosses, one
(`PinnedStr`) and several in one buffer (`Utf8Pack`). What calls through
those declarations — the library loaded, each section called, the records
read back — is `Xmip.Operate` in
[xmip-core-operate](https://github.com/IlleNilsson/xmip-core-operate), beneath
`Xmip.Surface`: a definition has no verb (ADR-0058, clause 1; ADR-0052,
amendment 2026-11-16). It compiles under C# 15's updated memory safety rules,
a preview: no type is `unsafe`; an `unsafe` block stands only around a
dereference or a framework call the runtime marks unsafe (`GCHandle.Free`);
and a member whose safety rests on its caller is marked `unsafe` itself —
`XmipStr`'s pointer, constructor and `Read`, `PinnedStr.Value`,
`Utf8Pack.Borrow`, and every raw pointer a header struct exposes.
`Module/` carries the module boundary's version and entrypoint, the status
codes, and the shapes a module fills: the host, the descriptor, the module
handle (`XmipModuleHandle`, the header's `XmipModule`), the table header,
the reader, the value kinds and section 14's Promote table and sink. What a
status code means and both boundaries at once are
written once, in Rust: `status_meaning::StatusMeaning`, what a status code
means — its header name, its line of English, whether it is retryable or
terminal — and `boundaries::Boundaries`, with `boundaries::library_file_name`,
how a platform names a module's library. `xmip abi` and `status` render
those, and .NET asks for them through `xmip_status_meaning_v1` and
`xmip_boundaries_v1` (`xmip_operate.h` section 28.3; `XmipStatusMeaning` and
`XmipBoundaries` here, `RuntimeWords` in Xmip.Operate), which
`Get-XmipAbi` and `ConvertFrom-XmipStatus` emit, so no face judges a code
on its own (ADR-0052, amendments 2026-09-24 and 2026-11-18). Whether a
module library loads and conforms is no longer judged here: the runtime's
one loader probes it, by the rule a node judges a module by, and
`xmip_module_probe_v1` (`xmip_operate.h` section 26) answers `xmip probe` and
`Get-XmipModuleDescriptor` alike (ADR-0012, amendment 2026-11-17). `dotnet/Xmip.Abi.Test`
compares the binding against the headers — every status, mood, counted kind
and section's values, the versions, the entrypoints and section 8's struct
layout — and holds those answers; `dotnet test dotnet/Xmip.Abi.Test` runs
them.

`dotnet/Xmip.Module` is the module side of `xmip_module.h` in .NET, as the
Rust crate is for Rust (ADR-0012 clause 2; ADR-0075): a developer writes a
unit in C# — a `PromoteUnit` today, header section 14 — and publishes it with
NativeAOT as a library a node opens through the C ABI exactly as it opens a
Rust module. It exports `xmip_create_module_v1` from its own assembly
(`Entrypoint`), and its build writes the module's `[[modules]]` entry beside
the library (`ModuleEntry`, run by `dotnet/Xmip.Module.Entry`, which the
package carries in `tools/`). Its package, `Xmip.Module`, carries
`Xmip.Abi`'s assembly inside it. `dotnet/Xmip.Module.Test` drives the table
as a node does; the guide is `doc/writing-a-module-in-dotnet.md`, and the
proof that a published library opens in a node is the runtime's
`ffi/loaded_promotion.rs`, run on purpose. `dotnet/Xmip.Templates` is the
`dotnet new` templates package beside it, `dotnet new xmip-module`: the two
packages a developer installs share this repository, its license and one
version, `XmipPackageVersion` in `dotnet/Directory.Build.props`, which the
template's module names `Xmip.Module` at as it is packed
(`test/DeveloperPackage.Test.ps1`).

This repository is Apache-2.0 ([LICENSE](LICENSE), [NOTICE](NOTICE)) — the
headers, the specification, the Rust crate, the bindings, `Xmip.Abi`,
`Xmip.Module` and `Xmip.Templates` — because a module links or compiles it into itself, and a
module stays its author's to license (ADR-0075; ADR-0061, amendment
2026-11-24). The rest of Xmip is AGPL-3.0-or-later. The crate depends on no
Xmip crate and the .NET projects on no Xmip project outside this repository,
so nothing AGPL is linked through it (`test/License.Test.ps1`).

`architecture.toml` carries the maturity; this file does not repeat it.
