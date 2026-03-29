# T12 — Canvas minimap, diagnostics, and group-frame renderer

## Phase
P2

## Goal
Move minimap and group frame visuals to canvas and reduce non-essential DOM layers. Diagnostics can remain HTML only for textual readouts, but scene-level diagnostic marks and minimap visuals should be canvas-driven.

## Why this task exists
This task is required to unblock the later real-canvas migration safely and to reduce the risk of breaking existing product behavior.

## Depends on
T10, T11

## Primary files
- `src/CanDoItAll.Components.CanvasLib/wwwroot/js-src/workbench/render/frames/**`
- `src/CanDoItAll.Components.CanvasLib/wwwroot/js-src/workbench/render/minimap/**`
- `src/CanDoItAll.Components.CanvasLib/wwwroot/js-src/workbench/render/diagnostics/**`
- `tests/CanDoItAll.Tests.Playwright/AppSmokeTests.cs`

## Feature IDs that must remain green
F06, F28, F29, F37, F40

## Implementation checklist
- Move minimap visuals to canvas and keep navigation behavior.
- Move group-frame visuals to canvas using the same anchor data the current runtime already has.
- Keep diagnostics text where HTML is useful, but move scene-level marks and mini-scene rendering to canvas.
- Verify screenshot parity and DOM reduction.

## Validation
- Minimap visuals are drawn on canvas and navigation still works.
- Group frames no longer render as a large set of positioned divs in runtime mode.
- Diagnostics remain readable and can still prove renderer counters.

## Done when
- More dense scene layers have moved off DOM and into canvas without loss of behavior.

## Notes
- Keep comments in source code in English.
- If the task changes shared canvas code, also validate PromptFactory and relevant sandbox surfaces.
- Do not suppress failing tests to get past this task.
