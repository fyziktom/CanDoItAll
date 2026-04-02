# Phase 01 Models Interfaces And Persistence Contracts

## Status

- `Completed`

## Objective

Create the storage domain model, persistence schema, routing contracts, and compatibility seam that all later provider and UI work depends on.

## Covered Inputs

- N001
- N002
- N003
- N004
- N007
- N008
- N009
- N010
- N011
- N013
- N014
- RQ-001
- RQ-002
- RQ-003
- RQ-004

## Prerequisites

- none

## Exact Source References

- C:\repositories\CanDoItAll/src/CanDoItAll.Infrastructure/Storage/WorkspaceStorage.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workbench/ProjectWorkbenchMetadata.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.SharedKernel/ProjectObjectContracts.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.Infrastructure/Configuration/AppOptions.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Security/SecurityModels.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.Migrations.Sqlite/Migrations/AppDbContextModelSnapshot.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.Migrations.PostgreSql/Migrations/AppDbContextModelSnapshot.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workbench/ProjectStructureCanvasCatalog.cs
- C:\repositories\CanDoItAll/candoitall-storage-driver-execution-bundle/requirements/03-default-routing-policy.md
- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Factory/PromptFactoryService.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs
- C:\repositories\CanDoItAll/candoitall-storage-driver-execution-bundle/architecture/01-target-solution.md
- C:\repositories\CanDoItAll/candoitall-storage-driver-execution-bundle/requirements/03-default-routing-policy.md
- C:\repositories\CanDoItAll/candoitall-storage-driver-execution-bundle/traceability/01-requirement-traceability.md

## Deliverables

- Typed storage domain models, provider capabilities, usage purposes, recommendation context, access descriptors, and storage-object reference model.
- Persistent storage catalog and routing-rule schema plan with SQLite + PostgreSQL migration instructions.
- Secret-link strategy for provider credentials.
- Compatibility seam plan for `IFileStore` / `IManagedArtifactStore` migration.
- Nested workstream notes under `workstreams/` for the Phase 01 slices.
- Nested workstream files listed below:
- `P1-WS01` - Storage domain contracts and capability model (`workstreams/01-p1-ws01-storage-domain-contracts-and-capability-model.md`)
- `P1-WS02` - Persistence, secret linkage, migrations, and bootstrap defaults (`workstreams/02-p1-ws02-persistence-secret-linkage-migrations-and-bootstrap-defaults.md`)
- `P1-WS03` - Routing rules and recommendation policy (`workstreams/03-p1-ws03-routing-rules-and-recommendation-policy.md`)
- `P1-WS04` - Legacy adapter and migration seam (`workstreams/04-p1-ws04-legacy-adapter-and-migration-seam.md`)

## Dependency Impact

- Phase 02 cannot implement drivers or access routes safely without the Phase 01 contracts.
- Phase 04 upload/adoption work will become inconsistent if storage-object references or routing rules are underspecified.
- Phase 03 test contracts are only meaningful when the domain and persistence model are stable.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Implement the storage domain models and interfaces defined in the architecture docs.
2. Define and persist storage catalog + routing rules + secret linkage.
3. Add the compatibility seam and mark old relative-path fields as temporary compatibility points where needed.
4. Prepare migration updates for both SQLite and PostgreSQL projects.
5. Update traceability if any schema or contract decision changes the touchpoint plan.

## Scope Exceptions

- Do not implement provider-specific runtime classes in Phase 01; only define their contracts and persistence boundaries.
- Do not claim UI completion here; Phase 01 is not the settings/workbench adoption phase.

## Do Not Do

- Do not hard-code provider-specific switch logic inside upload modules.
- Do not store raw provider secrets in plain text configuration or tables.
- Do not remove `IFileStore`/`IManagedArtifactStore` before the compatibility seam exists.

## Acceptance Checklist

- The new contract set can express FileSystem, IPFS, FTP, and future providers.
- The persistence model covers storage catalog records, routing defaults, and secret linkage.
- A migration path exists for both SQLite and PostgreSQL.
- Legacy-call-site migration strategy is explicit and testable.

## Proof Required

- `dotnet build src/CanDoItAll.Web/CanDoItAll.Web.csproj`
- Targeted migration/model update review in both migration projects.
- Traceability review confirming every Phase 01 requirement maps to a workstream.

## Browser Validation Logging

- N/A for direct UI layout proof in this phase.
- If any browser-visible schema-driven UI is changed early, reopen the phase and add a browser proof row before closure.

## Progression Gate

- Do not start Phase 02 until storage-object references, routing rules, and the compatibility seam are concrete.
- Do not start Phase 04 adoption if migrations or field ownership are still ambiguous.

## Suggested Agent Prompt

```text
Implement Phase 01 only.

Create the storage domain model, persistence schema plan, routing contracts, and compatibility seam.
Do not implement provider runtimes or UI adoption yet.
Keep comments in English.
Do not delete legacy interfaces until the adapter story is explicit.

Read this phase README, the nested workstream notes, the workbook inventories, and the execution checklist before changing code.
Update reviews/01-execution-report.md as you go.
Do not skip Playwright MCP proof when a browser-visible surface is touched.
```

