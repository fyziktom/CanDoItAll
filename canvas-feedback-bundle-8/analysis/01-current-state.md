# Current State

## Feedback and Live Baseline

- The feedback asks for a Visual Studio-like accordion toolbox, visible child items, readable light-surface contrast, slimmer selection panels, contextual hint affordances, and semantic file-type badges.
- A live Playwright baseline was captured on `http://127.0.0.1:5188/projects/10a2d1ce-ca8e-4c29-b56d-8483b60955f0/structure`.
- The first live failure was not missing accordion logic. The health floating window overlapped the toolbox and intercepted clicks, which made the group headers appear broken.
- After hiding the health window, the existing `Planning` toolbox group opened correctly. That means the main defect is default layout and visibility, not absence of expansion code.
- A live Excel file node was created in the browser baseline. The selection panel showed repeated subtype and upload signals, matching the feedback complaint.

## Relevant Implementation Files

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.ToolWindows.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.CreateCatalog.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor.css`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructureNodeDescriptor.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectWorkbenchModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructureCanvasCatalog.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\CanvasAdapters\ProjectStructureGraphAdapter.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\canvas-workbench.css`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectStructurePageTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\AppSmokeTests.cs`

## Current Behavioral Findings

- Toolbox groups already support single-open accordion behavior through `ExpandToolboxGroup` and related resolution methods.
- Search-driven expansion is already present and should not be broken by the fix.
- Selection-panel lead text and facts are currently assembled generically, which causes repetition on some node types.
- File nodes already have palette logic, but the selection panel still repeats file subtype and does not use the requested semantic badge styling.
