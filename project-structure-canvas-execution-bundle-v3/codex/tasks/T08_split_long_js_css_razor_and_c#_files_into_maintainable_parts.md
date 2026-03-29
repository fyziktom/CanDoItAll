# T08 — Split long JS, CSS, Razor, and C# files into maintainable parts

## Phase
P1

## Goal
Break the current monoliths into coherent source files with explicit ownership and line budgets, while keeping the public CanvasWorkbench API stable.

## Why this task exists
This task is required to unblock the later real-canvas migration safely and to reduce the risk of breaking existing product behavior.

## Depends on
T06, T07

## Primary files
- `src/CanDoItAll.Components.CanvasLib/wwwroot/js/canvasWorkbenchInterop.js`
- `src/CanDoItAll.Components.CanvasLib/wwwroot/js/canvas-floating-window.js`
- `src/CanDoItAll.Components.CanvasLib/wwwroot/canvas-workbench.css`
- `src/CanDoItAll.Components.CanvasLib/Components/CanvasWorkbench.razor`
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor`
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor.css`

## Feature IDs that must remain green
F01, F02, F05, F06, F08, F09, F30, F33, F39

## Implementation checklist
- Break `canvasWorkbenchInterop.js` into source fragments by concern and extract common helpers.
- Split `canvas-floating-window.js` if helper extraction is justified.
- Split `CanvasWorkbench.razor` into small internal components and code-behind.
- Split ProjectStructure page windows/dialogs into child components with their own scoped CSS.

## Validation
- canvasWorkbenchInterop public output is generated from js-src modules.
- CanvasWorkbench markup and code-behind are split into smaller internal components/partials.
- ProjectStructurePage floating windows and dialogs are extracted into dedicated child components with their own scoped CSS.
- No generated source file exceeds the agreed line budget except intentionally generated public bundles.

## Done when
- The codebase is measurably easier to navigate and reason about.
- Shared helpers for JS modules exist instead of being repeated across giant files.

## Notes
- Keep comments in source code in English.
- If the task changes shared canvas code, also validate PromptFactory and relevant sandbox surfaces.
- Do not suppress failing tests to get past this task.
