# Phase 1 Protected Areas

## Protected Area Summary

Phase 1 must treat the two canvas-heavy work surfaces as stable zones:

1. project structure management
2. prompt factory / complex prompt creation

These are the most interaction-rich parts of the product and already have stronger implementation and test coverage than the rest of the UI.

## Why They Are Protected

### 1. They are the most mature UI surfaces in the repo

Evidence from the current codebase:

- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor` is 1331 lines
- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor` is 1340 lines
- both pages use the shared `CanvasWorkbenchStage` and `CanvasWorkbench` system
- both have dedicated component tests
- both are covered in Playwright smoke/regression tests

### 2. Their main problems are now around them, not inside them

The surrounding shell is noisy:

- duplicate headers
- always-on global right rail
- too much vertical chrome
- limited focus mode

That surrounding noise can be improved without destabilizing the canvas behavior.

## Protected Files And Areas

Treat these as protected in phase 1 unless a change is purely superficial and clearly low risk:

- `src/CanDoItAll.ComponentKit/Components/CanvasWorkbench.razor`
- `src/CanDoItAll.ComponentKit/Components/CanvasWorkbenchStage.razor`
- `src/CanDoItAll.ComponentKit/wwwroot/canvas-workbench.css`
- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js`
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor`
- `src/CanDoItAll.Modules.Workbench/Components/ProjectStructureCanvas.razor`
- `src/CanDoItAll.Modules.Workbench/wwwroot/js/workbenchInterop.js`
- `src/CanDoItAll.Modules.Workbench/ProjectStructureCanvasCatalog.cs`
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs`
- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor`
- `src/CanDoItAll.Modules.Factory/FactoryDomain.cs`
- `src/CanDoItAll.Modules.Factory/PromptFactoryService.cs`

## Allowed Changes Around Protected Areas

These are allowed in phase 1:

- introduce a route-aware shell mode for protected routes
- reduce or remove the global shell right rail on protected routes
- compress the shell top bar on protected routes
- avoid repeating the full page introduction above the workbench stage
- adjust outer padding, max-width, and page framing around the workbench
- add documentation and tests that protect current behavior

## Forbidden Changes In Phase 1

Do not:

- redesign canvas node visuals
- change canvas action ids
- change canvas selection behavior
- change canvas keyboard/mouse gestures
- change JS interop contracts
- restructure the mirrored inspectors in a way that changes interaction behavior
- rewrite prompt build/session logic
- rewrite project graph mutation logic
- alter Playwright-covered canvas behavior as part of general cleanup

## Recommended Adapter / Wrapper Strategy

Use a shell-mode adapter instead of editing the protected pages deeply.

Recommended approach:

1. detect protected routes in `MainLayout.razor`
2. switch to a `FocusWorkbench` shell mode
3. in that mode:
   - keep tabs visible
   - keep route context compact
   - hide or collapse the global right rail
   - maximize usable width for the page body
4. leave the protected page internals and workbench stage content unchanged

## Required Regression Safety Measures

At minimum, protected-area changes must preserve:

- `tests/CanDoItAll.Tests.Components/ProjectStructurePageTests.cs`
- `tests/CanDoItAll.Tests.Components/PromptFactoryPageTests.cs`
- `tests/CanDoItAll.Tests.Playwright/AppSmokeTests.cs`

The most important Playwright checks to rerun are:

- `Direct_module_routes_and_workbench_surfaces_load_without_circuit_failure`
- `Prompt_factory_canvas_surface_loads_and_exposes_shared_chrome`
- `Structure_canvas_supports_inline_note_creation_editing_and_context_create_dialogs`

## Phase-1 Rule Of Thumb

If a proposed change touches how the user edits, selects, branches, creates, or opens items inside the two workbenches, it belongs to a later, dedicated canvas phase, not this one.

