# T01 — Overlay isolation and input ownership cleanup

## Phase
P0

## Goal
Ensure toolbox, floating windows, help/settings overlays, dialogs, and any HTML controls own their own pointer, wheel, click, and context-menu input. The canvas scene must ignore those events entirely.

## Why this task exists
This task is required to unblock the later real-canvas migration safely and to reduce the risk of breaking existing product behavior.

## Depends on
T00

## Primary files
- `src/CanDoItAll.Components.CanvasLib/wwwroot/js/canvasWorkbenchInterop.js`
- `src/CanDoItAll.Components.CanvasLib/Components/CanvasFloatingWindow.razor`
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor`
- `tests/CanDoItAll.Tests.Playwright/AppSmokeTests.cs`

## Feature IDs that must remain green
F01, F02, F03, F04, F13, F21, F30, F33, F35, F36

## Implementation checklist
- Replace partial overlay-target logic with a reliable overlay ownership contract that recognizes floating windows, toolbox, dialogs, popovers, and standard interactive descendants.
- Guard wheel, pointerdown, click, and context-menu routes so overlay-originated events do not reach the scene handlers.
- If needed, mark overlay roots with explicit data attributes instead of relying only on class selectors.
- Revalidate toolbox clicks, floating-window dragging, and help/settings/context interactions in browser tests.

## Validation
- Wheel scroll inside toolbox and health/selection windows changes their own scroll position and never changes canvas zoom.
- Pointer down inside overlays never starts scene pan, marquee, drag, or context routing.
- Accordion header clicks fire reliably in browser after the fix.
- No regression to context menu or help/settings overlay interactions.

## Done when
- The JS overlay filter includes floating windows and relevant HTML descendants, not only a partial selector list.
- Canvas zoom metrics remain unchanged when interacting with overlay content.

## Notes
- Keep comments in source code in English.
- If the task changes shared canvas code, also validate PromptFactory and relevant sandbox surfaces.
- Do not suppress failing tests to get past this task.
