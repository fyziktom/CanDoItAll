# 01-remove-persisted-workbench-sync-as-parallel-truth

## Status

- `Completed`

## Objective

Remove or quarantine the persisted system-managed projection graph so Workbench stops storing cross-module read models as if they were canonical project nodes.

## Covered Inputs

- `PWA-001`
- `PWA-009`
- `R-001`
- `R-002`
- `R-003`

## Prerequisites

- Confirm existing CRM/HR party-ownership fixes stay in place.
- Inventory every current SyncGraph contributor and every system-managed node/link kind.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectWorkbenchModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructureInvariantService.cs`

## Evidence Focus

- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:350-388`
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:398-424`
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:1962-2239`
- `src/CanDoItAll.Modules.Workbench/ProjectStructureInvariantService.cs:56-74`

## Deliverables

- A new assembly boundary such as ProjectStructureAssemblyService plus per-module projection contributors.
- Workbench canonical node storage that holds only user-authored canonical nodes or clearly named canonical carrier rows.
- No generic persisted hierarchy-like links for read-model-only structure.

## Dependency Impact

- Unblocks SB02 by clarifying what node persistence is actually canonical.
- Unblocks calendar and future plugin contributors from piggybacking on Workbench canonical tables.

## Validation Depth

- Static review of loader flow.
- Integration test asserting load paths do not create mirrored system-managed rows in canonical tables.
- Regression proof for structure and calendar rendering.

## Implementation Steps

- Create projection contributor contracts for Projects, Resources, Prompt Factory, Validation, TestLab, and future plugins.
- Move current SyncGraph projection building into those contributors.
- Compose the structure/calendar surface in memory or in dedicated read-model tables clearly separated from canonical node persistence.
- Delete or retire system-managed projection writes from Workbench_ProjectObjects and Workbench_ProjectObjectLinks.
- Adjust link semantics so hierarchy comes only from the parent relation, while semantic graph edges stay explicit.

## Do Not Do

- Do not keep writing projections into the same table under a new flag name.
- Do not treat assembled projection edges as canonical relations.

## Acceptance Checklist

- [x] GetStructureAsync and GetCalendarAsync no longer write cross-module projections into Workbench_ProjectObjects.
- [x] A repository/resource/validation/test-plan entry can appear in the surface without existing as a persisted canonical node row.
- [x] Hierarchy is represented once canonically.

## Proof Required

- Targeted integration tests for structure and calendar loads.
- Schema diff or code proof showing system-managed projection writes were removed or moved to dedicated read-model tables.
- Updated architecture review note confirming no parallel truth remains.

## Completion Notes

- Introduced `ProjectStructureAssemblyService` with projection contributors for hierarchy/phases, resources, prompt factory, validation, and test lab.
- `ProjectWorkbenchService` now assembles structure and calendar surfaces from canonical user-authored rows plus in-memory projections.
- Projected-node coordinate overrides now persist in `Workbench_ProjectProjectionLayouts` instead of promoting projected nodes into canonical Workbench tables.
- The retired `SyncGraphAsync` persistence path was removed from `ProjectWorkbenchService`.

## Architecture Resolution

- No parallel truth remains inside Workbench persistence. `Workbench_ProjectObjects` and `Workbench_ProjectObjectLinks` now hold only user-authored canonical nodes/links.
- Cross-module read-model nodes and links are assembled at read time through contributor contracts, so new plugins can project into the surface without writing mirrored canonical rows.
- Hierarchy semantics come from the project parent relation and assembled hierarchy links, not duplicated persisted read-model hierarchy rows.

## Proof Produced

- Runtime regression proof: `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProjectWorkbenchServiceIntegrationTests|FullyQualifiedName~ProjectWorkbenchSubtreeRecompositionIntegrationTests|FullyQualifiedName~ProjectStructureAgentIntegrationTests|FullyQualifiedName~ProjectStructureAgentApiIntegrationTests"` passed with `39/39` tests.
- Added integration coverage proving structure/calendar surfaces can include resource, validation, and test-plan projections with zero persisted Workbench node/link rows for the project.
- Added integration coverage proving projected node movement persists only `Workbench_ProjectProjectionLayouts` overrides and does not create canonical projection rows.
- Code/schema proof is in `src/CanDoItAll.Modules.Workbench/ProjectStructureAssemblyService.cs`, `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchSchemaInitializer.cs`, `src/CanDoItAll.Migrations.Sqlite/Migrations/20260405021055_AddWorkbenchProjectionLayouts.cs`, and `src/CanDoItAll.Migrations.PostgreSql/Migrations/20260405021055_AddWorkbenchProjectionLayouts.cs`.

## Browser Validation Logging

- If UI shape changes, capture structure and calendar routes after migration.

## Progression Gate

- Do not start SB02 until parallel truth is removed or quarantined.

## Suggested Agent Prompt

Implement SB01 exactly. Remove persisted SyncGraph-as-canonical behavior, introduce an assembly boundary, keep external surface contracts stable, add tests proving no mirrored system-managed nodes remain in canonical tables.
