# Xmip.Module

Write an Xmip Module's units in C# and publish them with NativeAOT as a
native library an Xmip node opens through the C ABI, exactly as it opens a
Rust module (`include/xmip_module.h` in xmip-core-abi; ADR-0075).

```powershell
dotnet new install Xmip.Templates
dotnet new xmip-module --name Orders
dotnet publish Orders/Orders --runtime linux-x64
```

The library and its `[[modules]]` entry, `Orders.module.xmip`, are in the
publish folder. A unit is a class:

```csharp
public sealed class OrderNumber : PromoteUnit
{
    public override string Name => "OrderNumber";

    public override IReadOnlyList<UnitSetting> Settings =>
        [new("member", SettingKind.Text, "The JSON member the order's number is read from.")
            { Default = "number" }];

    public override Promotion Open(StepSettings settings) => new Reading(settings.Text("member"));
}
```

and exports itself as the library loads:

```csharp
internal static class Exported
{
    [ModuleInitializer]
    internal static void Unit() => Export.Unit(new OrderNumber());
}
```

A library holds one unit. Built today: the Promote unit, header section 14.
The other capabilities follow their tables in the header. The guide is
`doc/writing-a-module-in-dotnet.md` in xmip-core-abi.

Apache-2.0, as all of xmip-core-abi is: what a module compiles into itself is
permissive, so the module stays its author's to license (ADR-0075). The rest
of Xmip is AGPL-3.0-or-later.
