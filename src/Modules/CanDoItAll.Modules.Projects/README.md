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

`ProjectsDbContext` maps Project, ProjectPhase, ProjectOptionSelection,

ProjectHierarchyLink, retained ProjectRetirementRecord, and ProjectCreationReservationRecord. Existing business

records retain their mappings; the lifetime addition requires the canonical migration.

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

and transaction. Projects saves its six owned record types, exits coordination,

commits, and releases the preparation scope before completion. Missing-project cleanup

and terminal recovery histories remain supported. Existing precommit filesystem

containment and reparse inspection remains validation; destructive byte effects stay

after the authoritative commit under the separate managed-binding gate.

The twelve-owner transfer context contract and other modules' direct project queries

remain dependent work. Canonical task identity and existing Agent recovery IDs are

retained. Agent revocation fences lease transitions by the durable attempt generation;

this admission layer does not change that behavior.

Project write admission is the tuple of canonical database profile, project ID and

persisted lifetime ID. Deletion retains that lifetime in Projects_ProjectRetirements

without a cascading foreign key. Same-ID creation gets a new lifetime; saved v2 project

JSON omits the local lifetime field and preserves every existing payload property.

Fresh inactive-profile import remains supported. Row transfer can preserve source
fields because the admission also binds the target profile explicitly.

LegacyAgentAccessBindingEligible records migration provenance for later Agent grant
binding. Only projects present during the first lifetime upgrade are marked eligible;
new creations and saved-package restores default to false. The marker is omitted
from compatible project JSON and cannot be changed by an ordinary editor. It does
not itself authorize a grant or infer the history of a missing project.

Migration 20260910183745_AddProjectLifetimes preserves existing business/recovery rows,
assigns a distinct lifetime to every live project and retains legacy recovery metadata
as null. Downgrade refuses retained retirements or lifetime-bound access recoveries.
Before an otherwise empty-evidence downgrade, stop writers and discard old admission
handles; re-upgrade assigns new lifetimes. Older binaries cannot enforce the new
metadata and must be drained before activating lifetime-aware writers.

Loaded editors retain ExpectedLifetimeId through the page's save clone and reject an

explicit stale lifetime. Legacy callers omitting that field remain compatible and

still need admission cutover. Project postcommit Search upsert captures the lifetime

at authoritative save, releases the original mutation scope, acquires the project

scope again, validates that lifetime, and stages Search on the exact same transaction.

A delayed old save cannot restore deleted search or overwrite a recreated project's

projection. Search failure remains a logged best-effort postcommit failure.

ProjectWriteAdmissionService has independent CaptureAsync and explicit enlisted

RequireForMutationAsync. The latter preserves the current transaction's read set;

callers must already hold the complete ordered project mutation scope before their

first read. It does not silently create a scope or capture a replacement admission.

Delayed operations must retain the originally captured admission through dispatch.

Pre-creation agent grants, multi-lifetime Agent revocation, remaining live foreign

writers and retained workflow/placement receipts require their dependent protocols.

This core does not claim that all deletion races are closed.

Agent project creation reserves the canonical profile, public project ID, new lifetime,
requester ID, and optional exact parent lifetime in Projects_ProjectCreationReservations
before its file-catalog grant. Only that typed reservation can consume the reserved ID;
consumption and Project insertion share the same owner transaction. Definite creation
rejection cancels the reservation before exact grant compensation. Ambiguous outcomes
retain the reservation. Normal public-ID reuse remains supported after closure and gets
a different lifetime. Ordinary deletion retains consumed/cancelled evidence and cancels
pending child reservations bound to the deleted parent. Reservations do not themselves
supply a durable tool invocation replay identity.

The Agent module owns catalog binding decisions over bulk Projects facts. The neutral
file store applies its optional catalog mutation policy under the existing file lock
on every save path and normalized read; standalone stores without that policy retain
their lower-library behavior. Product stores supply the policy explicitly. No database
transaction is carried through the file lock. Existing bound grants retain their exact
lifetime after retirement; late new bindings must name a live project or an active
reservation owned by that Agent. Legacy bare IDs bind only to upgrade-eligible current
rows, never to a later creation. Native mutation admission rechecks the captured lifetime on the actual owner
transaction. Catalog binding alone does not authorize a later effect; each delayed
producer must carry its saved source and hold the appropriate current-authority fence.

The complete model retains the CRM AccountConnectionProjects ProjectId cascade FK to

Projects_Projects. Projects' runtime model contains no CRM records. Canonical tasks

remain Workbench ProjectObjects and their binding/reference records; this module does

not create a separate task table or identity.

## Related Docs

- Repository overview: `README.md` at the repo root

- Current architecture: `docs/architecture/overview.md`
