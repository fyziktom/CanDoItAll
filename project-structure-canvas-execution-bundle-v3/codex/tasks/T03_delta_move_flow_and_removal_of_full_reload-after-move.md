# T03 — Delta move flow and removal of full reload-after-move

## Phase
P0

## Goal
Keep multi-node drag local. Persist moved positions in one batch and patch the in-memory surface instead of forcing ReloadSurfaceAsync after every move.

## Why this task exists
This task is required to unblock the later real-canvas migration safely and to reduce the risk of breaking existing product behavior.

## Depends on
T00, T02

## Primary files
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor`
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs`
- `tests/CanDoItAll.Tests.Components/ProjectStructurePageMoveTests.cs`
- `tests/CanDoItAll.Tests.Components/ProjectStructurePageSimpleMutationTests.cs`

## Feature IDs that must remain green
F08, F10, F28, F29, F30, F31, F38

## Implementation checklist
- Remove the normal-path `ReloadSurfaceAsync()` call after node moves.
- Patch the local surface positions directly using the move payload or returned IDs.
- Keep border adoption and selection retention working without a reload.
- Introduce a narrow fallback reload only when graph topology truly changed in a way that the local patch path cannot represent safely.

## Validation
- Node move callback updates positions, keeps selection, and adopts borders without a full surface reload.
- Only a batch move persistence call is made for a multi-node drag.
- No selection reset or viewport reset occurs after move.

## Done when
- HandleNodesMovedAsync no longer calls ReloadSurfaceAsync for the normal move path.
- A reload fallback exists only for explicit edge cases that truly change graph topology beyond a local patch.

## Notes
- Keep comments in source code in English.
- If the task changes shared canvas code, also validate PromptFactory and relevant sandbox surfaces.
- Do not suppress failing tests to get past this task.
