# CanDoItAll.Modules.Resources

## Purpose

Product module for resource records and reusable workspace materials.

## Project Type

- SDK: `Microsoft.NET.Sdk.Razor`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build src/Modules/CanDoItAll.Modules.Resources/CanDoItAll.Modules.Resources.csproj
```

## Dependencies

The authoritative project and package dependency list is in [CanDoItAll.Modules.Resources.csproj](CanDoItAll.Modules.Resources.csproj). This README focuses on the project's purpose, boundaries, and validation.

## Architecture Notes

This module owns product semantics for its bounded area. Keep business behavior here and expose it through typed services, Razor components, and module contracts. UI and transport adapters should call into these services instead of duplicating module logic.

`ResourcesDbContext` maps only resource metadata. Resource reads, writes, promotion,
reopening and Memory snapshots use its per-operation factory pinned to the host's
canonical database profile. Project existence/names and the bounded file-source list
come from Projects application queries. Storage continues to own bytes and access handles.

Complete migrations reuse the existing mapping without table, identifier or configuration
changes. The transfer residue check retains the explicit shared maintenance context
until the coordinated transfer boundary changes. Workbench projection/lifecycle readers
remain part of the subsequent cross-owner contract work.

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture/overview.md`
