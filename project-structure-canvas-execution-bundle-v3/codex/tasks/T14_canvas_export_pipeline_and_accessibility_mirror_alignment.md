# T14 — Canvas export pipeline and accessibility mirror alignment

## Phase
P2

## Goal
Update export and accessibility so they match the new runtime renderer. Export should compose canvas layers directly instead of cloning DOM/SVG. The accessibility mirror remains HTML and must continue to reflect selection and scene summary.

## Why this task exists
This task is required to unblock the later real-canvas migration safely and to reduce the risk of breaking existing product behavior.

## Depends on
T10, T11, T12, T13

## Primary files
- `src/CanDoItAll.Components.CanvasLib/Components/CanvasWorkbench.razor`
- `src/CanDoItAll.Components.CanvasLib/Components/AccessibilityMirrorLayer.razor`
- `src/CanDoItAll.Components.CanvasLib/wwwroot/js-src/workbench/export/**`
- `src/CanDoItAll.Components.CanvasLib/wwwroot/js/canvasWorkbenchInterop.js`
- `tests/CanDoItAll.Tests.Playwright/AppSmokeTests.cs`
- `tests/CanDoItAll.Tests.Components/AccessibilityMirrorLayerTests.cs`

## Feature IDs that must remain green
F21, F30, F37, F38

## Implementation checklist
- Replace DOM-clone export with renderer-owned canvas composition.
- Ensure the accessibility mirror remains accurate after scene DOM removal.
- Keep image export artifacts, clipboard behavior, and selection semantics correct.
- Update any tests or helper methods that assumed the old DOM-based export path.

## Validation
- Export image capture still produces the expected artifact after the DOM/SVG scene has been removed.
- The accessibility mirror stays in sync with selection and surface content.
- No regression to clipboard or context-menu flows caused by renderer changes.

## Done when
- The export path no longer depends on the old DOM clone/foreignObject approach for the main scene.

## Notes
- Keep comments in source code in English.
- If the task changes shared canvas code, also validate PromptFactory and relevant sandbox surfaces.
- Do not suppress failing tests to get past this task.
