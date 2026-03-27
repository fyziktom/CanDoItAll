
# Repository surface map

Use this document as the fast lookup sheet for the main files that Codex should inspect first.

## Shared contracts and workbench core

- `src/CanDoItAll.SharedKernel/ProjectObjectContracts.cs`  
  Current node-family contracts and object type definitions.
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs`  
  Persistence model, service logic, and workbench record shape.
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchSchemaInitializer.cs`  
  Schema bootstrap and required columns.

## Project Structure canvas

- `src/CanDoItAll.Modules.Workbench/ProjectStructureCanvasCatalog.cs`  
  Hard-coded create catalog for project structure nodes.
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor`  
  Main page markup and inspector surface.
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor.css`  
  Project structure page styling.
- `src/CanDoItAll.Modules.Workbench/CanvasAdapters/ProjectStructurePlacementPolicy.cs`  
  Placement logic that must be fixed for side-aware child positioning.
- `src/CanDoItAll.Modules.Workbench/CanvasAdapters/ProjectStructureActionCatalogAdapter.cs`  
  Compact ring and status-action definitions.
- `src/CanDoItAll.Modules.Workbench/CanvasAdapters/ProjectStructureGraphAdapter.cs`  
  Graph transformation and grouping/border-adjacent logic.
- `src/CanDoItAll.Modules.Workbench/Calendar/ProjectCalendarAdapter.cs`  
  Existing calendar projection from node dates.
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectCalendarPage.razor`  
  Calendar UI.

## Prompt Factory canvas

- `src/CanDoItAll.Modules.Factory/PromptFactoryCanvasCatalog.cs`  
  Prompt Factory catalog including current components submenu configuration.
- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor`  
  Prompt Factory page UI and floating inspector patterns.
- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor.css`  
  Prompt Factory page styles.
- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.Catalog.cs`  
  Catalog action handling and component insert pipeline.

## Shared canvas UI and interop

- `src/CanDoItAll.ComponentKit/Components/FloatingInspectorHost.razor`
- `src/CanDoItAll.ComponentKit/Canvas/Graph/FloatingInspectorHost.cs`
- `src/CanDoItAll.ComponentKit/Components/CanvasWorkbench.razor`
- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js`
- `src/CanDoItAll.ComponentKit/wwwroot/canvas-workbench.css`

These files are the natural home for the shared floating tool-window host and many screenshot-visible behaviors.

## Reusable domain modules

- `src/CanDoItAll.Modules.Resources/ResourceModels.cs`
- `src/CanDoItAll.Modules.Resources/Pages/ResourcesPage.razor`
- `src/CanDoItAll.Modules.Workspace/WorkspaceModels.cs`
- `src/CanDoItAll.Modules.Workspace/ProviderExecution.cs`
- `src/CanDoItAll.Modules.Security/SecurityModels.cs`

## Manager/runtime helpers

- `tools/CanDoItAll.Manager/LaunchProfileSettingsResolver.cs`
- `tools/CanDoItAll.Manager/WorkspaceRuntimeProcessTools.cs`

## Existing test anchors

- `tests/CanDoItAll.Tests.Components/*.cs`
- `tests/CanDoItAll.Tests.Integration/*.cs`
- `tests/CanDoItAll.Tests.Unit/*.cs`
- `tests/CanDoItAll.Tests.Playwright/*.cs`
