# CanDoItAll.Plugins.Abstractions

## Purpose

Shared plugin contracts for descriptors, package manifests, identifiers, grants, connections, OAuth metadata, settings renderers, capability interfaces, host-tool requests, workspace file access, storage placement, project-structure read access, HTTP access, and execution events.

## Project Type

- SDK: `Microsoft.NET.Sdk`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build src/plugins/Abstractions/CanDoItAll.Plugins.Abstractions/CanDoItAll.Plugins.Abstractions.csproj
```

## Dependencies

The authoritative project and package dependency list is in [CanDoItAll.Plugins.Abstractions.csproj](CanDoItAll.Plugins.Abstractions.csproj). This README focuses on the project's purpose, boundaries, and validation.

## Architecture Notes

Keep this project contract-only. It is the typed boundary between plugin implementations, `CanDoItAll.Modules.Plugins`, and workflow execution. Avoid adding infrastructure or UI behavior here; those belong in the plugin module or concrete plugin assemblies.

Manifest validation should stay strict so plugins cannot silently declare duplicate identifiers, unsupported capabilities, or incomplete settings/rendering metadata.

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture/overview.md`
