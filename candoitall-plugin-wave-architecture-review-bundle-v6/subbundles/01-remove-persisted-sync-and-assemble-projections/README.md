# 01-remove-persisted-sync-and-assemble-projections

## Status

- `Prepared for Codex execution`

## Objective

Stop persisting system-managed cross-module projection nodes and links into canonical Workbench storage.

## Covered Inputs

- `PW6-001`

## Prerequisites

- Inventory every current SyncGraph contributor, command path, and read path that depends on IsSystemManaged rows.
- Decide whether projection nodes are assembled in memory or stored in dedicated read-model tables.

## Exact Source References

- `/mnt/data/unpacked_phase5_current/CanDoItAll-canonical-model-refactor/src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs`
- `/mnt/data/unpacked_phase5_current/CanDoItAll-canonical-model-refactor/src/CanDoItAll.Modules.Workbench/ProjectStructureInvariantService.cs`

## Evidence Focus

- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:350-388`
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:398-425`
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:1767-1833`
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:1962-2240`

## Deliverables

- Projection contributor contracts for Projects, Resources, Prompt Factory, Validation, TestLab, and future plugins.
- Assembly service that composes the surface from canonical carrier nodes plus read-only contributors.
- No canonical Workbench write path that mirrors foreign module state into ProjectObjectRecord/ProjectObjectLinkRecord.

## Dependency Impact

- Unblocks the rest of the refactor because it removes the biggest remaining split source of truth.
- Creates the correct base for future plugin-contributed read-only nodes.

## Validation Depth

- Static review of all load and command paths.
- Integration tests proving no system-managed rows are created/updated during structure and calendar reads.
- Regression proof that projection nodes still render correctly in the surface.

## Implementation Steps

- Create an assembly boundary and per-module projection contributor interface.
- Move current SyncGraph projection building logic behind contributor implementations.
- Change GetStructure/GetCalendar/command flows to assemble read-only nodes instead of persisting them into canonical node tables.
- Remove or quarantine IsSystemManaged rows from canonical storage.

## Do Not Do

- Do not keep writing projections into the same canonical tables under a renamed flag.
- Do not let projection-only nodes become legal targets for canonical lifecycle mutations.

## Acceptance Checklist

- [ ] GetStructureAsync and GetCalendarAsync no longer mutate canonical Workbench rows just to show foreign module data.
- [ ] At least one non-Workbench module entity appears in the surface without being stored as a canonical ProjectObjectRecord.
- [ ] Projection-only nodes are clearly read-only and not valid canonical mutation targets.

## Proof Required

- Targeted integration tests for structure and calendar loading.
- Code diff showing SyncGraph writes are removed from canonical flow.
- Updated review note confirming that parallel truth is gone.

## Browser Validation Logging

- If the visible structure changes, capture structure and calendar routes after migration.

## Progression Gate

- Do not start SB02 until the parallel truth is removed or explicitly quarantined.

## Suggested Agent Prompt

Implement SB01 exactly. Replace persisted SyncGraph-as-canonical behavior with an assembly boundary and contributor model. Preserve the visible surface contracts where possible, but stop writing foreign module state into canonical Workbench node tables.
