# CanDoItAll.Modules.Plugins

## Purpose

Product module for plugin catalog, installation state, capability grants, OAuth connections, plugin logs, package activation, runtime plugin service registration, host-tool recipes, and plugin settings UI.

## Project Type

- SDK: `Microsoft.NET.Sdk.Razor`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build src/Modules/CanDoItAll.Modules.Plugins/CanDoItAll.Modules.Plugins.csproj
```

## References

Project references:

- `../CanDoItAll.AgentFramework.Core/CanDoItAll.AgentFramework.Core.csproj`
- `../CanDoItAll.Components.BaseLib/CanDoItAll.Components.BaseLib.csproj`
- `../CanDoItAll.Infrastructure/CanDoItAll.Infrastructure.csproj`
- `../CanDoItAll.Modules.Security/CanDoItAll.Modules.Security.csproj`
- `../CanDoItAll.Plugins.Abstractions/CanDoItAll.Plugins.Abstractions.csproj`
- `../CanDoItAll.SharedKernel/CanDoItAll.SharedKernel.csproj`

Framework references:

- None

Direct package references:

- `Microsoft.AspNetCore.Components.Web (10.0.4)`
- `Microsoft.EntityFrameworkCore (10.0.4)`
- `Microsoft.Extensions.Hosting.Abstractions (10.0.7)`
- `Microsoft.Extensions.Http (10.0.4)`

## Architecture Notes

This module is the runtime host for plugin governance. Keep capability checks explicit through `PluginGrantEvaluator`; workflow executors and OAuth services should fail predictably when a plugin is disabled, missing a declared capability, or missing a grant.

Bundled plugin implementations live under `src/plugins`. External package activation flows through manifest validation and runtime registrars, not ad hoc assembly loading from product pages.

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture-beta.md`
