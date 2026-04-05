# 01-remove-persisted-workbench-sync-as-parallel-truth

## Status

- `Prepared for Codex execution`

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

- `/mnt/data/unpacked_current/CanDoItAll-canonical-model-refactor/src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs`
- `/mnt/data/unpacked_current/CanDoItAll-canonical-model-refactor/src/CanDoItAll.Modules.Workbench/ProjectStructureInvariantService.cs`

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

- [ ] GetStructureAsync and GetCalendarAsync no longer write cross-module projections into Workbench_ProjectObjects.
- [ ] A repository/resource/validation/test-plan entry can appear in the surface without existing as a persisted canonical node row.
- [ ] Hierarchy is represented once canonically.

## Proof Required

- Targeted integration tests for structure and calendar loads.
- Schema diff or code proof showing system-managed projection writes were removed or moved to dedicated read-model tables.
- Updated architecture review note confirming no parallel truth remains.

## Browser Validation Logging

- If UI shape changes, capture structure and calendar routes after migration.

## Progression Gate

- Do not start SB02 until parallel truth is removed or quarantined.

## Suggested Agent Prompt

Implement SB01 exactly. Remove persisted SyncGraph-as-canonical behavior, introduce an assembly boundary, keep external surface contracts stable, add tests proving no mirrored system-managed nodes remain in canonical tables.
