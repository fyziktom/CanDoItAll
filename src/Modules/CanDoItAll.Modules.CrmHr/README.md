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

The Web host exposes the supported HTTP slice at `/api/crm-hr`. Web owns route binding and status mapping; this module's application services continue to own validation, persistence, audit, search-index, activity, and lifecycle side effects. Do not add direct `DbContext` writes or scenario-specific seed behavior to the Web adapter.

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture-beta.md`
- CRM-HR HTTP API: `docs/crm-hr-api.md`
