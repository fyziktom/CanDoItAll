# Canonical dependency model and compatibility boundary

## Status

- `Completed`
- `2026-04-13`: canonical dependency handling now flows through `ProcessDependencyCompatibilityBridge`, direct legacy scalar mirror writes were removed from save/publish/workspace paths, and targeted integration plus component proof passed on the live repository.

## Objective

- Replace the current dual dependency meaning with one canonical dependency model and quarantine any legacy compatibility behavior behind a single explicit boundary.

## Covered Inputs

- `U003` Canonicality and maintainability concerns.
- `BRQ-003` Canonical dependency model.
- `BRQ-004` Explicit compatibility boundary.
- `F001` Dual dependency representation.

## Prerequisites

- `01-baseline-characterization-and-live-gap-reconciliation` passed.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessDefinitionEnums.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessDefinitionEntities.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessDefinitionEntityConfigurations.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessDefinitionEditorModels.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessCanvasBranching.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.Support.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.Persistence.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.Reads.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.Runtime.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessesServiceIntegrationTests.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProcessCanvasSurfaceFactoryTests.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProcessWorkspaceTests.cs

## Deliverables

- A single canonical dependency representation used by core authoring, persistence, clone, read, and runtime paths.
- A named compatibility adapter or migration bridge that is the only place allowed to interpret legacy dependency fields if they still temporarily exist.
- Removal of scattered fallback logic from helpers and query/runtime code.
- Regression coverage proving canonical dependency round-tripping and compatibility behavior.

## Dependency Impact

- Subbundles 03-10 all depend on this being correct.
- If dependency meaning is still ambiguous after this phase, later save, publish, and runtime proof cannot be trusted.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Choose the canonical dependency model explicitly, with the explicit dependency row/collection shape as the source of truth.
2. Refactor the definition/editor/read/runtime helpers so they consume the canonical dependency shape instead of reconstructing meaning from multiple fields.
3. Create one small compatibility adapter or migration bridge for legacy dependency fields if data compatibility still requires them.
4. Update save/load/publish/clone/runtime/read paths so the canonical model flows end to end.
5. Add or update tests that prove the canonical dependency shape survives round trips and that legacy compatibility is isolated.

## Scope Exceptions

- This phase does not yet optimize transaction boundaries or differential persistence.
- This phase may keep legacy fields temporarily only if the compatibility boundary is explicit and small.

## Do Not Do

- Do not keep two-way sync logic spread across multiple helpers.
- Do not leave runtime or read-side code reading legacy fields directly.
- Do not route dependency meaning through free-text or inferred ordering hacks.

## Acceptance Checklist

- Core code uses one canonical dependency representation.
- Any remaining legacy dependency handling is isolated behind one compatibility boundary.
- Runtime, read, and persistence logic no longer each reconstruct dependency meaning differently.
- Regression tests prove canonical round-trip behavior.

## Proof Required

- Focused integration tests around save/load/publish/clone/runtime dependency behavior.
- Any needed component tests proving canvas/workspace surfaces still reflect the canonical dependency graph.
- Updated execution report showing that dependency fallback logic was narrowed to one boundary.

## Browser Validation Logging

- N/A for this phase unless the authoring UI must visibly change to reflect the canonical shape.
- If any authoring UI changes are required immediately, record them but keep full UI closure for the later workspace phase.

## Progression Gate

- There is one canonical dependency model, scattered fallback logic is removed, and any legacy handling is isolated enough that later phases can treat dependency meaning as stable.

## Suggested Agent Prompt

```text
Implement only subbundle 02. Canonicalize process dependencies so one representation governs authoring, persistence, clone, reads, and runtime. Quarantine any legacy-field handling behind one explicit adapter or migration bridge, and stop before transaction or workspace refactors.
```
