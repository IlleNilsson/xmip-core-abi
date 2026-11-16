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
`xmip-core-report` keeps and a node's build of xmip-core-operate answers.
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
`Module/` carries the module boundary, its probe and the probe's judgment of
whether a library loaded at all (`ModuleProbe.Result.Unloadable`) and
whether a module conforms, and `StatusMeaning`, what a status code means.
`AbiBoundaries` says both boundaries at once. `xmip abi`, `status` and
`probe` render those and `Get-XmipAbi`, `ConvertFrom-XmipStatus` and
`Get-XmipModuleDescriptor` emit them, so neither face judges a code or a
module on its own (ADR-0052, amendment 2026-09-24). `dotnet/Xmip.Abi.Test`
compares the binding against the headers — every status, mood, counted kind
and section's values, the versions, the entrypoints and section 8's struct
layout — and holds those answers; `dotnet test dotnet/Xmip.Abi.Test` runs
them.

`architecture.toml` carries the maturity; this file does not repeat it.
