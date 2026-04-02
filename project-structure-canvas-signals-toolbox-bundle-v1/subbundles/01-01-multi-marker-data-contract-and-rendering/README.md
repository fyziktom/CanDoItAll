# Subbundle 01-01: Multi-Marker Data Contract And Rendering

## Status

- `Completed`

## Objective

- Upgrade project-structure nodes from single-marker behavior to additive markers while preserving compatibility for legacy single-marker readers.

## Covered Inputs

- `N006`

## Prerequisites

- Prepared-stage bundle validator passes.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectWorkbenchMetadata.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectWorkbenchModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\CanvasAdapters\ProjectStructureGraphAdapter.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Canvas\Workbench\CanvasWorkbenchNode.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\runtime\workbench\02-layout-and-legacy-render.js`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\runtime\workbench\06-canvas-renderers.js`

## Deliverables

- Typed marker-set metadata contract.
- Service update path that adds or clears markers without collapsing the full set.
- Canvas and DOM node rendering that can show multiple markers compactly.
- Selection-summary wording that no longer implies only one marker exists.

## Dependency Impact

- Downstream toolbox proof is untrustworthy until additive markers are both stored and rendered correctly.

## Validation Depth

- Focused automated validation plus one browser smoke on a node that receives multiple markers.

## Implementation Steps

1. Add typed metadata classes for ordered marker sets.
2. Extend structure-node and canvas-node projections with marker collections.
3. Update marker mutation logic to synchronize metadata and primary-marker compatibility fields.
4. Update node renderers and selection summaries to show multiple markers.

## Do Not Do

- Do not introduce a database migration for this phase.
- Do not keep the full marker set only in UI state.

## Acceptance Checklist

- Multiple markers can exist on one node at the same time.
- Legacy single-marker fields still expose a primary marker.
- Node rendering visibly shows more than one marker when present.

## Proof Required

- Focused test results for marker metadata synchronization.
- Browser smoke that applies at least two markers to one node and confirms both remain visible.

## Browser Validation Logging

- Route used: `http://127.0.0.1:5500/projects/2eac2cae-5138-437d-ac57-1a1b142ebccb/structure`
- Selected node: `test`
- Applied markers during proof: `Question`, then `Risk`
- Visible result: selection panel showed `Paused, Question, Risk` and both marker tiles were active at once
- Screenshot: `C:\repositories\CanDoItAll\output\playwright-mcp\signals-window-desktop-proof.png`
- Cleanup: temporary proof markers were removed after validation

## Progression Gate

- Do not begin subbundle `02` until additive marker storage and rendering are trusted.
- Gate result: `Passed`

## Suggested Agent Prompt

- Implement additive markers through metadata with a primary-marker compatibility bridge, then prove that both the data path and visible node rendering survive repeated marker application.
