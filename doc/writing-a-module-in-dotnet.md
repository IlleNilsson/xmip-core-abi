# Writing a Module in .NET

A Module's units can be written in C#. Published with NativeAOT, the project
is a native library that a node opens through the C ABI exactly as it opens
a Rust module: the same header, `include/xmip_module.h`, the same loader and
the same descriptor rule, and the node cannot tell which it loaded (ADR-0075).
The unit runs inside the node's process, so the C ABI is its way in; code on
another machine reaches Xmip over its operator API instead. What the module
compiles into itself — `Xmip.Module`, `Xmip.Abi` and what `dotnet new`
writes — is Apache-2.0, so the module is yours to license as you choose.

Built today: the Promote unit, header section 14. The other capabilities
follow their tables in the header.

## What you need

- The .NET 11 SDK.
- NativeAOT's linker: on Windows the MSVC build tools (the C++ workload's
  linker and the Windows SDK); on Linux `clang` and the C library's headers.
- `Xmip.Templates` and `Xmip.Module`: reserved, not yet on nuget.org. Until
  they are, pack them from the estate into a folder and name it as a package
  source (step 1).

## 1. Install the template

From a clone of the estate, the two packages into one folder:

```powershell
dotnet pack module/foundation/abi/dotnet/Xmip.Module/Xmip.Module.csproj --configuration Release --output .local-work/package/nuget
dotnet pack module/foundation/abi/dotnet/Xmip.Templates/Xmip.Templates.csproj --configuration Release --output .local-work/package/nuget
dotnet new install .local-work/package/nuget/Xmip.Templates.0.1.0.nupkg
```

and that folder as a package source in the `nuget.config` beside your
projects, with nuget.org after it.

## 2. Make a Module

```powershell
dotnet new xmip-module --name Orders
dotnet test Orders/Orders.slnx
```

`Orders/Orders` is the Module, `Orders/Orders.Test` its test. The unit is a
class:

```csharp
public sealed class OrderNumber : PromoteUnit
{
    public override string Name => "OrderNumber";

    public override IReadOnlyList<UnitSetting> Settings =>
    [
        new("member", SettingKind.Text, "The JSON member the order's number is read from.")
        {
            Default = "number",
        },
    ];

    public override Promotion Open(StepSettings settings) => new Reading(settings.Text("member"));
}
```

- **`Name`** is what a step entry's `unit` names it by. Nothing reads meaning
  from it: what the unit is called as is its class, `PromoteUnit`.
- **`Settings`** declares what a step entry may give, written into the
  Module's manifest; the node reads a step's settings through it and fills in
  each default before the unit opens.
- **`Open`** is called once for each step entry, never per Message, with what
  the entry gives. It returns a `Promotion`, whose `Promote` reads a
  Message's content as a `Stream` and hands each value to `Promoted`: `Text`,
  `Number`, `Boolean` or `Binary`, under the context property it is promoted
  into. A node calls `Promote` from any thread, many at once, so it keeps
  nothing between Messages.
- **A refusal** is a `RefusedException` with why, in one sentence: from
  `Open` it refuses the step's settings, from `Promote` the Message's
  content. Anything else thrown is a defect in the unit.

The unit exports itself as the library loads, and a library holds one unit:

```csharp
internal static class Exported
{
    [ModuleInitializer]
    internal static void Unit() => Export.Unit(new OrderNumber());
}
```

## 3. Publish it

```powershell
dotnet publish Orders/Orders --runtime linux-x64
```

for the platform the node runs on (`win-x64`, `linux-arm64`, `osx-arm64`
...). The publish folder holds the library, named as that platform names
one (`libOrders.so`, `Orders.dll`, `libOrders.dylib`), and the Module's
`[[modules]]` entry, `Orders.module.xmip`:

```toml
[[modules]]
name = "Orders"
start = true

[modules.manifest]
offline = true

[modules.manifest.identity]
name = "Orders"
version = "0.1.0"

[[modules.manifest.capabilities]]
capability = "promote"
execution_host = "c-abi"
trusted_required = false

[modules.manifest.entrypoint]
library_path = "Orders"
symbol = "xmip_create_module_v1"

[[modules.manifest.units]]
name = "OrderNumber"
capability = "promote"
stages = ["receive"]
offline = true

[[modules.manifest.units.settings]]
name = "member"
kind = "text"
presence = "default"
default = "number"
meaning = "The JSON member the order's number is read from."
```

`library_path` names the Module, not a file: the node names the file as its
own platform does, so one configuration serves every platform. The version
is the project's `Version`, which a configuration pins.

## 4. A node opens it

The entry goes into the cluster's `cluster.xmip`, and the library beside the
node's configuration, where `library_path` is read from. A node opens the
library through its loader (`ffi/loaded_module.rs`), and the Promote unit
through its table (`ffi/loaded_promotion.rs`). The runtime's own proof does
exactly that with the template's library:

```powershell
$env:XMIP_PROMOTE_LIBRARY = (Resolve-Path -Path Orders/Orders/bin/Release/net11.0/linux-x64/publish/libOrders.so).Path
cargo test --package xmip-core-runtime --features dynamic-loading loaded_promotion -- --ignored
```

What is not built yet: a node runs no step entry, so a Receive Location
attaching a Promotion is refused as the node starts, saying this runtime
runs no compiled unit yet; and a node's startup opens only contract and
Work Process libraries, so it refuses a `[[modules]]` entry claiming
`promote` until it runs one.
