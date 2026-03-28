# Current State

## Relevant Repo Surfaces

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor`
  The workbench page already owns the toolbar, canvas workflows, selection persistence, and page-level feedback. The left toolbar content currently exposes `Inspector`, `Health`, and `Blocks` only.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectWorkbenchModels.cs`
  `ProjectWorkbenchService` already loads structure surfaces, persists node coordinates, and updates node positions through `MoveObjectAsync`, but it has no batch subtree recomposition seam.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\CanvasAdapters\ProjectStructurePlacementPolicy.cs`
  The placement policy only resolves new-node placement near a source or parent. It does not rebalance existing descendants.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\wwwroot\js\workbenchInterop.js`
  The canvas renderer uses fixed bounds per shape: `circle 104x104`, `pill 196x64`, and the default card `204x80`. That makes deterministic collision checks feasible without round-tripping DOM measurement.
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectStructurePageTests.cs`
  The page already has component coverage for toolbar buttons and selection-window workflows.
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProjectWorkbenchServiceIntegrationTests.cs`
  The service already has integration coverage for structure loading, reparenting, links, and persistence.
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\AppSmokeTests.cs`
  The repo already uses Playwright smoke coverage on the project structure page and floating windows.

## Observed Layout Behavior

- `ProjectStructurePage` always keeps a selected node, which is the right interaction contract for a selection-scoped recomposition command.
- `ProjectStructurePlacementPolicy.ResolveCreatePlacement` chooses child coordinates by projecting away from a single anchor. That is appropriate for local create actions but it does not solve long-term subtree compaction.
- `ProjectWorkbenchService.GetDefaultPosition` uses lane-style defaults by object type:
  - phases: fixed left lane
  - work items and meetings: middle lane
  - repositories and files: right lane
  - prompt steps: far-right lane
  This reinforces directional growth when many nodes are added over time.
- `MoveObjectAsync` persists one node at a time. Recomposition needs one batch operation so the subtree moves coherently and validation can reason about one persisted result instead of many piecemeal updates.

## Known Algorithm Options

- `Reingold-Tilford tidy tree`
  Widely used for rooted tree drawing because it keeps siblings contiguous and produces deterministic subtree ordering. It is a strong base for this problem because the requested command is scoped to a selected parent-child subtree.
- `Buchheim-Junger-Leipert linear-time Walker improvement`
  Useful when a tidy-tree style layout must scale to larger trees without quadratic passes. It matters if the selected subtree grows large enough that repeated recomposition should stay cheap.
- `Fruchterman-Reingold force-directed layout`
  Widely used for general graph layout, but it is a poor fit here because it is intentionally free-form, more likely to disturb user-adjusted layouts, and less predictable for a manual “recompose only this subtree” command.
- `Sugiyama layered layout and radial Sugiyama variants`
  Strong for DAGs and hierarchical graphs with crossing reduction, but they still optimize for directional layering more than for circular space usage around one anchored root.

## Recommendation From Current-State Analysis

- Use a deterministic, parent-child radial subtree layout inspired by tidy-tree ordering rather than a force-directed graph solver.
- Keep the selected node anchored and treat the rest of the canvas as fixed obstacles.
- Persist the final coordinates through the workbench service, not in page-only state, because the command must survive reloads and MCP-driven refreshes.
