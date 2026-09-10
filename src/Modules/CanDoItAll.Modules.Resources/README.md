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
until the coordinated transfer boundary changes. Workbench receives typed projection
and scope facts from ResourcesService; connector kind/subtype resolution stays inside
Resources and configuration JSON does not cross that query boundary. Project-name
lookups use the bulk Projects query without the file-source catalog's reference cap.

Ordinary projection reads use the independent owner factory. Explicit mutation
projection reads enlist through the shared transaction coordinator so Workbench
validation retains its caller's relational snapshot and transaction read set.

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture/overview.md`
