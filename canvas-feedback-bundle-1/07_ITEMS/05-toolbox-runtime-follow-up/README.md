# Item 05: Toolbox runtime follow-up

## Covered notes

- `N008`
- `N009`
- `N010`
- `N011`
- `N012`

## Objective

Close the runtime gaps left after the baseline toolbox refactor: duplicate chrome, accordion behavior, search-result usability, and icon rendering.

## Execution checklist

- Suppress the standard floating-window header for the toolbox so only the dark internal surface remains.
- Add shared headerless floating-window grid behavior so body-only windows stay height-constrained.
- Replace passive toolbox disclosure markup with explicit accordion state in page code.
- Keep matching groups open during search.
- Render toolbox item icons through the shared icon pipeline instead of raw token text.
- Constrain the toolbox window height and validate search-result movement with real wheel input.

## Implemented in

- `src/CanDoItAll.Components.CanvasLib/Components/CanvasFloatingWindow.razor`
- `src/CanDoItAll.Components.CanvasLib/wwwroot/canvas-workbench.css`
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.ToolWindows.cs`
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor`
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor.css`
- `src/CanDoItAll.Components.BaseLib/Components/Identity/FontAwesomeIconCatalog.cs`
- `src/CanDoItAll.Web/Components/App.razor`
- `tests/CanDoItAll.Tests.Components/CanvasFloatingWindowTests.cs`
- `tests/CanDoItAll.Tests.Components/ProjectStructurePageTests.cs`
- `tests/CanDoItAll.Tests.Playwright/AppSmokeTests.cs`

## Validation

- `CanvasFloatingWindowTests`
- `ProjectStructurePageTests`
- `Project_structure_feedback_fixes_are_validated_in_browser`
- `Project_structure_artifacts_capture_required_canvas_evidence`
- `C:\repositories\CanDoItAll\output\playwright\feedback1\02-toolbox-accordion-search.png`

## Status

`Done and validated`
