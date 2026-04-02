# Current State

## Existing Marker Flow

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectWorkbenchModels.cs` persists only `MarkerIcon`, `MarkerTone`, and `MarkerLabel` on `ProjectObjectRecord`.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectWorkbenchModels.cs` maps those single fields onto `ProjectStructureNode`.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\CanvasAdapters\ProjectStructureGraphAdapter.cs` maps that same single marker into `CanvasWorkbenchNode`.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor` applies marker actions through `ProjectWorkbenchService.UpdateObjectMarkerDetailedAsync(...)`, which currently overwrites the previous marker.

## Existing Right-Click Marker Rendering

- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\runtime\workbench\03-interaction-and-state.js` renders second-layer marker presets using a fixed `.cw-node__badge--menu` badge.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\css\workbench\scene\04-scene-and-nodes.css` keeps those marker badges at a fixed width and height, which is compatible with a glyph-only enlargement.

## Existing Node Rendering

- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\runtime\workbench\02-layout-and-legacy-render.js` draws one marker badge beside progress and priority in DOM-based node rendering.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\runtime\workbench\06-canvas-renderers.js` draws one marker badge in canvas-mode node rendering.

## Existing Floating Window Pattern

- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Components\Core\CanvasFloatingWindow.razor` already provides draggable and minimizable floating windows.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.ToolWindows.cs` already manages the standard blocks toolbox window state.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\Components\ProjectStructure\ProjectStructureToolbarActions.razor` already exposes toolbar toggles for floating windows.

## Compatibility Opportunity

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectWorkbenchMetadata.cs` already supports typed metadata in `MetadataJson`.
- A marker-set payload can be added there without forcing a database schema migration, while the legacy single-marker columns can continue carrying the primary marker for compatibility.
