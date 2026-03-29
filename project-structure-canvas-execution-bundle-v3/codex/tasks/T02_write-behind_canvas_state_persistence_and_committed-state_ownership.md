# T02 — Write-behind canvas state persistence and committed-state ownership

## Phase
P0

## Goal
Move view-state persistence off the hot path. JS should keep transient interaction state locally and only send committed/idle snapshots. C# should avoid immediate SaveViewState and full surface refresh for every selection, zoom, or transient viewport change.

## Why this task exists
This task is required to unblock the later real-canvas migration safely and to reduce the risk of breaking existing product behavior.

## Depends on
T00, T01

## Primary files
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor`
- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor`
- `src/CanDoItAll.Components.CanvasLib/wwwroot/js/canvasWorkbenchInterop.js`
- `src/CanDoItAll.Components.CanvasLib/Canvas/CanvasWorkbenchContracts.cs`
- `tests/CanDoItAll.Tests.Components/ProjectStructurePageTests.cs`
- `tests/CanDoItAll.Tests.Components/PromptFactoryPageTests.cs`

## Feature IDs that must remain green
F01, F02, F08, F09, F30, F33, F36, F37

## Implementation checklist
- Introduce a commit-only or write-behind state coordinator for shared-canvas state persistence.
- Change ProjectStructurePage state callbacks so transient scene changes update local UI state without immediate database persistence.
- Apply the same persistence discipline to PromptFactory shared-canvas callbacks.
- Keep selection/window restoration behavior intact by persisting committed snapshots only.

## Validation
- No DB or service persistence occurs during pointermove, drag, or continuous wheel zoom.
- Selection persistence still survives reload/refresh once the interaction commits.
- PromptFactory no longer eagerly persists canvas UI state on every shared callback.

## Done when
- ProjectStructurePage and PromptFactoryPage both use a commit-only or write-behind persistence path.
- Instrumentation proves the state publish and persistence counts dropped materially from baseline.

## Notes
- Keep comments in source code in English.
- If the task changes shared canvas code, also validate PromptFactory and relevant sandbox surfaces.
- Do not suppress failing tests to get past this task.
