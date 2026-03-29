# T13 — Canvas node renderer phase A with hot-zones and HTML overlay escape hatches

## Phase
P2

## Goal
Move runtime node cards to canvas. Preserve selection, drag, double-open, collapse, and compact-path copy by using hit-tested hot-zones. Keep HTML overlays only for active editors, dialogs, context menus, and any parity-critical control that should not live inside the canvas draw pass.

## Why this task exists
This task is required to unblock the later real-canvas migration safely and to reduce the risk of breaking existing product behavior.

## Depends on
T10, T11, T12

## Primary files
- `src/CanDoItAll.Components.CanvasLib/wwwroot/js-src/workbench/render/nodes/**`
- `src/CanDoItAll.Components.CanvasLib/wwwroot/js-src/workbench/interaction/hit-testing/**`
- `src/CanDoItAll.Components.CanvasLib/wwwroot/js-src/workbench/overlays/**`
- `src/CanDoItAll.Components.CanvasLib/wwwroot/js/canvasWorkbenchInterop.js`
- `tests/CanDoItAll.Tests.Playwright/AppSmokeTests.cs`
- `tests/CanDoItAll.Tests.Components/ProjectStructurePageTests.cs`

## Feature IDs that must remain green
F03, F05, F08, F09, F10, F11, F12, F13, F14, F15, F21, F22, F23, F24, F25, F31, F32, F37, F38

## Implementation checklist
- Draw runtime nodes on canvas, including text, palette, status indicators, and compact-path visuals.
- Implement hot-zones for node body, collapse affordance, and compact-path copy.
- Keep active editors, context menus, and any parity-critical control in HTML overlays anchored from canvas geometry.
- Introduce a low-detail rendering mode for zoomed-out or large scenes.

## Validation
- Runtime nodes are no longer rendered as a DOM element per node in the main workbench scene.
- Hit-zones cover at least: node body, collapse affordance, compact-path copy button, and node open/double-activation.
- Context menu, inline note editing, quick action dialog, and attachment open flows still work.
- A zoomed-out LOD path exists so very dense graphs do not draw full text for every node.

## Done when
- ProjectStructurePage uses a real canvas scene for nodes, links, frames, and minimap, while still preserving feature parity through overlays and hit regions.

## Notes
- Keep comments in source code in English.
- If the task changes shared canvas code, also validate PromptFactory and relevant sandbox surfaces.
- Do not suppress failing tests to get past this task.
