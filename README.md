# xmip-core-abi

The Xmip application binary interface (ABI): the stable boundary used by the runtime, operator surfaces, and loadable Modules.

The C header and its specification are normative. The Rust crate is a convenience binding over that boundary; it must not introduce Rust-specific types into the ABI.

The .NET binding is here too: `dotnet/Xmip.Abi`, one class library declaring both `include/xmip_module.h` and `include/xmip_operate.h` for every operator surface — the cli, the PowerShell module and the GUI reference it as a project and bind nothing themselves (ADR-0014, amendment of 2026-08-26). `dotnet/Xmip.Abi.Tests` compares it against the headers; `dotnet test dotnet/Xmip.Abi.Tests` runs them.

Beside the binding is what every .NET surface shares (ADR-0052): `dotnet/Xmip.Surface` — `IOperatorSurface` with its two implementations, `NativeOperator` over the binding and `SnapshotOperator` over a published snapshot; the scope tree, its rollup and the worst leaf beneath a scope (`ScopeTree`); runtime discovery by one rule (`RuntimeLibrary`: `Xmip:RuntimeLibrary`, else `XMIP_RUNTIME_LIBRARY`, else beside the executable); a status said in English once (`English`); the surface a host chose in its TOML (`SurfaceChoice`); and the one TOML reader (`TomlDocument`). The GUI hosts, the cli and the PowerShell module are thin faces over it. `dotnet/Xmip.Surface.Test` covers the tree, discovery, the English and the snapshot surface over a fixture; `dotnet test dotnet/Xmip.Surface.Test` runs them.

Status: planned, with the Rust binding, the .NET binding and the module manifest model already present.
