# Source Artifacts

- `A01`
  Source: inline screenshot supplied in the user thread.
  Summary: the current project structure canvas grows mostly downward and slightly right from the root node, leaving a large amount of unused space around the root while descendant chains become long and hard to inspect without panning.
- `A02`
  Source: user request in the thread.
  Summary: the feature must be manual, toolbar-triggered, selection-scoped, collision-free, and limited to position changes only.
- `A03`
  Source: repository audit during preparation.
  Paths:
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor`
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectWorkbenchModels.cs`
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\CanvasAdapters\ProjectStructurePlacementPolicy.cs`
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\wwwroot\js\workbenchInterop.js`
  Summary: the page already exposes a toolbar and selection-driven workflows, the workbench service persists node coordinates, the placement policy only handles create-time placement, and the JS canvas exposes fixed shape-based node bounds that can support deterministic collision checks.
