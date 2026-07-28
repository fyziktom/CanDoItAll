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

## Dependencies

The authoritative project and package dependency list is in [CanDoItAll.Modules.Plugins.csproj](CanDoItAll.Modules.Plugins.csproj). This README focuses on the project's purpose, boundaries, and validation.

## Architecture Notes

This module is the runtime host for plugin governance. Keep capability checks explicit through `PluginGrantEvaluator`; workflow executors and OAuth services should fail predictably when a plugin is disabled, missing a declared capability, or missing a grant.

Bundled plugin implementations live under `src/plugins`. External package activation flows through manifest validation and runtime registrars, not ad hoc assembly loading from product pages.

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture-beta.md`
