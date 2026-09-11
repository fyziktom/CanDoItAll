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

Development Agent diagnostics use `AiAgentService` for the original AI-party identity
list and binding facts. These explicit diagnostic reads include archived parties and
all binding states, preserve database name ordering, and use the CRM owner context.
They return only the fields already exposed by the development routes; they do not
repair or refresh the technical catalog. Web retains its development/local-or-authorized
access gate, synchronization sequence, response envelopes and status mapping.

## Ordinary planning reads

The CRM-owned `CrmPlanningAgentRuntimeToolProvider` exposes `crm_planning_search`
and `crm_planning_summary_get` to admitted interactive Project Structure, Gantt and
Projects chats. Both use the existing bounded, privacy-filtered
`ICrmHrAgentQueryService` DTOs. They return identity, safe summaries and availability;
they do not reserve capacity, assign work or expose confidential records. Business
text remains marked as untrusted data. The Tooling project reference provides the
neutral provider/metadata contract; CRM does not depend on the AgentFramework
product module or MAF SDK implementation.

To enable an ordinary planner, open its existing Agents catalog editor, enable
tool use and the required Project Structure read scope, then assign **CRM Planning
Search** and/or **CRM Planning Summary** in **Capabilities**. In **Memory**, explicitly
enable **Allow CRM source reads**, and save. Capability assignment and CRM source
permission are independent prerequisites. The latter preserves the existing
`memory.allowedSourceScopes` representation and does not enable a memory provider,
change invocation mode, grant HR administration or assign any tools automatically.
Start a new authorized chat turn after changing grants; a resumed invocation cannot
gain newly enabled tools.

Each proposal binds the original capability ID, provider, admitted session and exact
project source/lifetime in its versioned preparation. Dispatch and saved-result
disclosure recheck current authority against that source. The catalog lease spans
the CRM read; project/profile checks complete before returning data. Saved search
disclosure uses one current bounded owner search, so changed visibility or a record
falling outside the original search result window denies that saved disclosure.
It never substitutes new data for the original checkpoint. Missing legacy source
evidence requires a new authorized turn. Managed HR identities and purposes remain
separate; Process, Scheduler and automatic callers receive no grant from this adapter.

This implements the missing authorized CRM read attachment in Foundation FEAT-120
and SURF-008 using CON-057. Existing Resource projections and bounded native/Storage
content reads remain the Resource planning path; MAT-011 is not a blanket permission
manifest.

## Assignment staging boundary

Work-item assignees live in Workbench's `Workbench_WorkAssignments` table. CRM
retains project participation, staffing, capacity, rates and the mixed project-move
receipt. Its compatibility facade stages cross-role transitions, replacements and
cleanup with the Work owner on one serializable transaction. Assignment identities
are locked across both owners; cross-role edits retain the same ID, phase and
opportunity. Party merge stages Work party/affiliation rewrites and native task
revisions before CRM's intermediate saves, retaining the existing audit entry.
Public contracts contain typed facts and commands, without persistence entities or
contexts. CRM supplies only Party and affiliation facts needed by Work mutations.

Combined assignment and workforce reports use a fixed read-only SQL union before
joins, predicates, totals, ordering and paging. `WorkItemAssignee` remains an explicit
constant discriminator. The query maps no writable Work entity into CRM's runtime
model. The InMemory provider has a separate explicit test path; it does not establish
cross-context transaction atomicity.

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

### Assignment and staffing lifetime binding

Participation, Work assignments and project-linked Staffing requests retain nullable project-lifetime provenance. New assignment saves and replacements, including an empty replacement, require the caller's captured `ProjectWriteAdmission`. Existing editors retain the displayed selection through asynchronous work; legacy selections without a binding require refresh. Opportunity conversion uses the selected project snapshot or the exact new-project commit receipt for both its assignment commit and compensation.

Global and orphan history remains readable. Current project/allocation reports filter another incarnation before SQL paging; historical Party reports retain old rows and do not label them with a replacement project's name. Reference backfill is limited to unambiguous current projects and never creates actor permission. Assignment move receipts bind both source and target lifetimes. Recorded-source cleanup does not require the source project to remain live.
