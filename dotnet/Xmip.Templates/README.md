# Xmip.Templates

`dotnet new` templates for Xmip (ADR-0075).

```powershell
dotnet new install Xmip.Templates
dotnet new xmip-module --name Orders
dotnet test Orders/Orders.slnx
dotnet publish Orders/Orders --runtime linux-x64
```

`xmip-module` gives a Module with one Promote unit, `OrderNumber`, which
promotes a JSON order's `number` into the Message's context at receive, and
its test. Published, the project is a native library compiled ahead of time,
which an Xmip node opens through the C ABI exactly as it opens a Rust module,
and beside it its `[[modules]]` entry for the cluster's configuration,
`Orders.module.xmip`. The unit is written over `Xmip.Module`, which the
module names at this package's own version: the two are packed together, from
one repository, at one version.

Apache-2.0, as `Xmip.Module` is: what `dotnet new` writes becomes the
developer's own project, to license as they choose (ADR-0075). The rest of
Xmip is AGPL-3.0-or-later.
