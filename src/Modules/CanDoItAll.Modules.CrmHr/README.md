# CanDoItAll.Modules.CrmHr

## Purpose

Product module for parties, CRM accounts, recruiting, workforce/staffing, AI-agent party bindings, and CRM/HR records used by process assignments.

## Project Type

- SDK: `Microsoft.NET.Sdk.Razor`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build src/Modules/CanDoItAll.Modules.CrmHr/CanDoItAll.Modules.CrmHr.csproj
```

## Dependencies

The authoritative project and package dependency list is in [CanDoItAll.Modules.CrmHr.csproj](CanDoItAll.Modules.CrmHr.csproj). This README focuses on the project's purpose, boundaries, and validation.

## Architecture Notes

This module owns product semantics for its bounded area. Keep business behavior here and expose it through typed services, Razor components, and module contracts. UI and transport adapters should call into these services instead of duplicating module logic.

Directory, Workforce, CRM, and Recruiting use the shared typed `PagedRecordBrowser`; party-backed routes compose it through the module-owned `PartyRecordBrowser` adapter. Queries perform source paging with deterministic ordering, the route catalogue owns an opt-in bounded card-results scroll, and complete record workspaces open in controlled full-size dialogs without displacing or recreating the catalogue. Recruiting separates application, interview, lifecycle, and conversion work into server-rendered dialog tabs. Picker-dialog consumers keep the browser's default non-bounded scroll behavior.

The Agents route projects AgentFramework-owned identities instead of maintaining a second technical catalog. It joins the invalidation-aware `IAgentReferenceDataProvider` snapshot to the durable `AiResourceBinding.TechnicalAgentId` mapping and CRM-owned governance fields, then filters and pages that immutable composite snapshot in memory. The catalogue renders the shared `AgentSelectionCard`; selecting a card opens a CRM-HR read-only dialog, while technical edits remain in AgentFramework. The scoped composite snapshot expires after 20 seconds and is cleared by the shared AgentFramework invalidation signal both before and after successful directory synchronization, so search, validation filters, paging, and direct record lookup do not issue a database query on every interaction or retain a pre-synchronization join.

Technical Agent projection is a CRM-owned writer. The AgentFramework adapter supplies
one immutable catalog snapshot with canonical profile, organization scope and catalog
revision. CRM commits the bindings and a per-source cursor atomically under its
PostgreSQL gate. Equal-revision identical input is replayable; contradictory input is
rejected, and an older revision cannot overwrite newer state, including an empty
catalog. Technical availability/provenance is separate from human-maintained Party
fields and governance. Missing or superseded Agents remain identifiable and unavailable;
repair does not silently revive them. Legacy unprovenanced bindings stay readable.

Technical catalog saves commit before projection/audit/search completion. Failures in
those later obligations preserve the committed Agent and Party identities in the
typed outcome; retry must reconcile those identities instead of creating replacements.
The complete PostgreSQL migration adds binding provenance and the projection cursor.
Downgrade is permitted only while this new evidence is empty; retained provenance or
even an empty-catalog cursor blocks destructive rollback.

The Web host exposes the supported HTTP slice at `/api/crm-hr`. Web owns route binding and status mapping; this module's application services continue to own validation, persistence, audit, search-index, activity, and lifecycle side effects. Do not add direct `DbContext` writes or scenario-specific seed behavior to the Web adapter.

## Assignment staging boundary

Direct task-assignment mutations derive the final assignment DTOs from CRM's current
ChangeTracker, including unsaved additions and excluding deleted/detached rows. The
single staging helper used by save, replace, delete, node cleanup, and project move
enters the existing mutation transaction only while calling Workbench's data-only
assignment bridge. Workbench saves through its explicit enlisted owner context; CRM
then performs its final save and retains commit ownership. The public bridge exposes
no DbContext or foreign persistence entity.

CRM uses its explicit runtime model for parties, staffing, recruiting and current
participation records. The complete migration model preserves the physical
AccountConnectionProjects ProjectId cascade FK; the runtime model retains the
scalar reference and its indexes without mapping writable Project entities.
Project existence and ordinary labels use Projects owner queries. The assignment
history report is an explicit read-only integration query: a parameterized SQL
projection joins only the Project name needed for database ordering, then pages
the existing immutable result DTO. It preserves database collation, tie ordering,
UTC dates and missing-project labels without loading a whole name catalog or
imposing a new project-count limit. No foreign entity is tracked or writable.
Project admission, retired-ID fencing, complete lifecycle/transfer integration,
and canonical participation semantics remain separate required work.

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture/overview.md`
- CRM-HR HTTP API: `docs/crm-hr-api.md`
