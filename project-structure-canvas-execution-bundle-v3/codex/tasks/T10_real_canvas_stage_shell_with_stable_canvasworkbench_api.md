# T10 — Real canvas stage shell with stable CanvasWorkbench API

## Phase
P2

## Goal
Introduce a true canvas-based stage for the runtime workbench without breaking the CanvasWorkbench public parameters/events. The stage should contain real <canvas> layers plus an HTML overlay root and accessibility mirror.

## Why this task exists
This task is required to unblock the later real-canvas migration safely and to reduce the risk of breaking existing product behavior.

## Depends on
T01, T02, T06, T07, T08

## Primary files
- `src/CanDoItAll.Components.CanvasLib/Components/CanvasWorkbench.razor`
- `src/CanDoItAll.Components.CanvasLib/Canvas/CanvasWorkbenchContracts.cs`
- `src/CanDoItAll.Components.CanvasLib/wwwroot/js-src/workbench/**`
- `src/CanDoItAll.Components.CanvasLib/wwwroot/js/canvasWorkbenchInterop.js`
- `src/CanDoItAll.Components.Sandbox/Components/Pages/CanvasBenchmark.razor`

## Feature IDs that must remain green
F01, F21, F30, F33, F34, F37, F38, F40

## Implementation checklist
- Introduce a real canvas stage shell while keeping the public `CanvasWorkbench` parameters/events stable.
- Add devicePixelRatio-aware canvas sizing, resize observation, and frame scheduling.
- Support a rollout mode or feature flag if needed so parity can be proven gradually.
- Keep HTML overlay and accessibility layers available above the canvas.

## Validation
- The runtime stage contains actual canvas layers sized with devicePixelRatio-aware attributes.
- CanvasWorkbench events (selection, nodes moved, create, context action, node opened, state changed) still flow through the same public surface contract.
- A feature flag or staged rollout path exists until parity is proven.

## Done when
- ProjectStructure and PromptFactory can still render through CanvasWorkbench while the renderer implementation becomes canvas-based internally.

## Notes
- Keep comments in source code in English.
- If the task changes shared canvas code, also validate PromptFactory and relevant sandbox surfaces.
- Do not suppress failing tests to get past this task.
