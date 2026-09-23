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

Complete migrations reuse the owner mapping; a nullable project-lifetime reference is
added without changing identifiers or connector configuration. The transfer residue check retains the explicit shared maintenance context
until the coordinated transfer boundary changes. Workbench receives typed projection
and scope facts from ResourcesService; connector kind/subtype resolution stays inside
Resources and configuration JSON does not cross that query boundary. Project-name
lookups use the bulk Projects query without the file-source catalog's reference cap.

Ordinary projection reads use the independent owner factory. Explicit mutation
projection reads enlist through the shared transaction coordinator so Workbench
validation retains its caller's relational snapshot and transaction read set.

Editors and file-browse promotion capture the selected project's profile and lifetime
from Projects facts. Resource saves validate that exact admission under the existing
project mutation lock and the owner's actual transaction. Configuration and file-access
validation stay outside the database transaction. The stored lifetime is reference
provenance, not an actor permission or a producer grant. Loading a saved resource never
rebinds it to today's project with the same public ID.

Global catalog and resource-ID reads retain orphan and retired references. Current
project filters, Workbench projection/scope facts and project-scoped Memory snapshots
exclude rows bound to another lifetime or lacking a binding. Global labels do not use a
recreated project's name for those rows. Explicit resource-plus-project routes require
the same lifetime; a resource-only route still opens the historical record. An operator
can select a current project explicitly to bind an editable historical resource. Exact
resource cleanup remains available after project retirement.

Search remains a post-commit effect. Its short transaction rechecks the captured project
lifetime before staging the Search owner write; a retirement between saves can therefore
surface a failure after resource metadata was committed. Activity retains its historical
reference behavior. This is not an atomic outbox or an automatic retry policy. FileTools
continues to enforce current actor access for storage-object promotion.

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture/overview.md`
