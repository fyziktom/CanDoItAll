# CanDoItAll.Modules.Projects

## Purpose

Product module for project portfolio records, phases and options, project-to-project
hierarchy, party integration, file portfolios, and project-facing pages and services.

## Project Type

- SDK: `Microsoft.NET.Sdk.Razor`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build src/Modules/CanDoItAll.Modules.Projects/CanDoItAll.Modules.Projects.csproj
```

## Dependencies

The authoritative project and package dependency list is in [CanDoItAll.Modules.Projects.csproj](CanDoItAll.Modules.Projects.csproj). This README focuses on the project's purpose, boundaries, and validation.

## Architecture Notes

This module owns project portfolio records and their product semantics. Keep that
behavior here and expose it through typed services, Razor components, and module
contracts. The Workbench module owns canonical Project Structure nodes, workbench state,
and node mutations; this module consumes those capabilities through typed bridge
contracts instead of duplicating them.

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture-beta.md`
