# T11 — Canvas links renderer and geometry-based link hit model

## Phase
P2

## Goal
Replace SVG link rendering with real canvas drawing and maintain link visibility, culling, and context semantics through geometry/hit data rather than DOM elements.

## Why this task exists
This task is required to unblock the later real-canvas migration safely and to reduce the risk of breaking existing product behavior.

## Depends on
T10

## Primary files
- `src/CanDoItAll.Components.CanvasLib/wwwroot/js-src/workbench/render/links/**`
- `src/CanDoItAll.Components.CanvasLib/wwwroot/js-src/workbench/interaction/hit-testing/**`
- `src/CanDoItAll.Components.CanvasLib/wwwroot/js/canvasWorkbenchInterop.js`
- `tests/CanDoItAll.Tests.Playwright/AppSmokeTests.cs`

## Feature IDs that must remain green
F10, F21, F37, F38

## Implementation checklist
- Replace SVG link drawing with canvas link drawing.
- Store per-link geometry metadata for hit testing and context behavior.
- Cull invisible links and batch redraw through the new runtime scheduler.
- Remove now-unused SVG link layer dependencies from the runtime stage.

## Validation
- Links no longer contribute SVG DOM nodes in runtime workbench mode.
- Link routing and visibility still match the current visual model closely enough for screenshots to pass.
- Context actions and selection behavior that depend on link geometry still work.

## Done when
- The scene DOM count drops because links are no longer SVG elements.

## Notes
- Keep comments in source code in English.
- If the task changes shared canvas code, also validate PromptFactory and relevant sandbox surfaces.
- Do not suppress failing tests to get past this task.
