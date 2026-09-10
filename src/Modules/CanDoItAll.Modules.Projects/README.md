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

## Runtime persistence boundary

`ProjectsDbContext` maps only Project, ProjectPhase, ProjectOptionSelection, and
ProjectHierarchyLink with their existing table, key, index, and property mappings.
Normal project operations, recent activity, record queries, and file-scope project
lookups use this owner context. Factories bind to the host's immutable canonical
database profile. The complete AppDbContext remains the sole schema and migration
authority; this context performs no independent schema initialization.

`ProjectRecordQueryService.GetForMutationAsync` and `GetManyForMutationAsync` are
explicit coordinated reads: they require the caller's active coordinated scope,
share its database connection and transaction, and finish before returning. Ordinary
query methods use independent factory contexts. Bulk GetMany retains its complete
requested-ID result set; only ListReferences has the separate bounded catalog limit.

Project deletion uses ProjectsDbContext and the data-only deletion participant
contract. Projects owns one Serializable transaction and the sorted union of project,
hierarchy, and participant keys. Workbench, Agent access, Search, and Storage staging
each save through a fresh explicitly enlisted owner context on that same connection
and transaction. Projects saves its four owned record types, exits coordination,
commits, and releases the preparation scope before completion. Missing-project cleanup
and terminal recovery histories remain supported. Existing precommit filesystem
containment and reparse inspection remains validation; destructive byte effects stay
after the authoritative commit under the separate managed-binding gate.

The twelve-owner transfer context contract and other modules' direct project queries
remain dependent work. Runtime owner contexts do not establish durable project
admission or prevent retired ProjectId reuse. Canonical task identity and existing
Agent recovery IDs are retained; Agent revocation now fences lease transitions by
the durable attempt generation.

The complete model retains the CRM AccountConnectionProjects ProjectId cascade FK to
Projects_Projects. Projects' runtime model contains no CRM records. Canonical tasks
remain Workbench ProjectObjects and their binding/reference records; this module does
not create a separate task table or identity.

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture/overview.md`
