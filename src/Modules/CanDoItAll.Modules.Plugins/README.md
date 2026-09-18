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

Runtime stores use `PluginsDbContext`, whose model explicitly contains only installations, capability grants, connections, OAuth connections, OAuth sessions, and plugin logs. Its pooled factory binds to the host's immutable canonical database profile. The context reuses existing mappings and application-managed GUID stamping without adding optimistic concurrency enforcement.

The complete application model retains these mappings for migrations. This boundary does not create a separate database or copy saved manifests, settings, grants, or OAuth references. The canonical migration chain creates the schema; runtime stores and bootstrap do not issue separate Plugin schema DDL. Existing historical extra indexes are preserved.

OAuth session and connection changes retain their existing save boundaries. Secret material continues through the Security vault; persisted vault references and parameterized connection/log queries keep their existing formats.

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture/overview.md`
