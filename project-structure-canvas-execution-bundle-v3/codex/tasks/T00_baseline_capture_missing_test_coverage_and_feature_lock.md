# T00 — Baseline capture, missing test coverage, and feature lock

## Phase
P0

## Goal
Create a hard baseline before changing behavior. Capture current screenshots, DOM counts, persistence counters, and fill the most dangerous test gaps (especially toolbox expand/collapse, tooltip, single-line rows, overlay wheel isolation, and PromptFactory shared-canvas smoke).

## Why this task exists
This task is required to unblock the later real-canvas migration safely and to reduce the risk of breaking existing product behavior.

## Depends on
None

## Primary files
- `tests/CanDoItAll.Tests.Components/ProjectStructurePageTests.cs`
- `tests/CanDoItAll.Tests.Components/CanvasWorkbenchTests.cs`
- `tests/CanDoItAll.Tests.Components/CanvasFloatingWindowTests.cs`
- `tests/CanDoItAll.Tests.Components/PromptFactoryPageTests.cs`
- `tests/CanDoItAll.Tests.Playwright/AppSmokeTests.cs`
- `src/CanDoItAll.Components.Sandbox/Components/Pages/CanvasBenchmark.razor`
- `src/CanDoItAll.Components.CanvasLib/wwwroot/js/canvasWorkbenchInterop.js`

## Feature IDs that must remain green
F01, F02, F03, F04, F33, F34, F35, F36, F40

## Implementation checklist
- Inventory the current ProjectStructure and PromptFactory browser scenarios and map them to feature IDs.
- Add missing Playwright coverage for toolbox accordion open/close, row compactness, tooltip/title, and wheel isolation.
- Expose or log baseline renderer metrics from the current runtime (DOM node count, state publish counters, zoom count).
- Capture baseline screenshots and benchmark outputs and store them in predictable output paths.

## Validation
- Add browser tests for toolbox group toggle open/close with aria-expanded proof.
- Add browser tests for tooltip/title and single-line toolbox rows.
- Add browser tests proving wheel inside toolbox/floating window does not zoom the canvas.
- Capture baseline screenshots for ProjectStructurePage, toolbox states, PromptFactory canvas, and CanvasBenchmark results page.
- Add instrumentation readout or JS-exposed counters for DOM node count, renderer kind, state publish counts, and zoom events.

## Done when
- The repo has a repeatable baseline suite and screenshot set before renderer work begins.
- Known toolbox bugs are reproducible in automated tests, not only by anecdote.

## Notes
- Keep comments in source code in English.
- If the task changes shared canvas code, also validate PromptFactory and relevant sandbox surfaces.
- Do not suppress failing tests to get past this task.
