# Current State

## Repo Truth

- `SummaryTile` already supports `Compact` and help-tooltip mode in `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Cards\SummaryTile.razor`, but it still renders label and value as separate stacked text blocks.
- The process workspace already has denser shells and scroll containment from the previous request, but the summary row still uses card-style tiles that cost more height than a badge-style metric strip.
- `ProcessCanvasToolbarActions.razor` exposes only select, delete, selection, and toolbox buttons. There is no recomposition command surface yet.
- `ProcessCanvasSurfaceFactory.Coordinates.cs` still assigns default positions with simple heuristics:
  - steps follow a mostly linear `140 + index * 280` X progression
  - roles sit in a fixed left rail
  - branch nodes derive from direct dependents
  Those defaults do not actively solve collisions when definitions become denser or branch-heavy.
- `ProcessWorkspace.Canvas.cs` already persists moved node coordinates back into `ProcessDefinitionEditorModel` and schedules persistence. That is the correct seam for persisted recomposition results.
- The current product already contains one substantial C# recomposition reference in `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\CanvasAdapters\ProjectStructureSubtreeRecompositionEngine.cs`, but it is workbench-specific and radial, not process-specific.
- CanvasLib has a `LayoutEngine` namespace and `ViewportController`, but no shared persisted recomposition contract yet.
- CanvasLib JavaScript currently includes transient collision-resolution helpers in `wwwroot\js\runtime\workbench\01-foundation.js`. That logic is render-time support, not the persisted C# recomposition requested here.

## Immediate Architecture Observations

- Collision-only and spacing-only commands are generic enough to belong in a shared CanvasLib recomposition layer.
- Smart process recomposition needs process-domain semantics such as mainline sequencing, roles, and branching, so the orchestration of that strategy should stay in the processes module even if it uses shared geometry primitives.
- The safest way to satisfy the managed SQLite requirement is to drive recomposition through the existing product workflow and then verify the persisted coordinates in the database after the product writes them.
