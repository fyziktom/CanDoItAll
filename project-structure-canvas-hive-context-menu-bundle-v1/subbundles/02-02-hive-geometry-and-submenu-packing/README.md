# 02-hive-geometry-and-submenu-packing

## Status

- `Completed`
- `Browser proof captured on 2026-04-01 for the root hive, Blocks submenu hive, and keyboard shortcut path into the Delivery block composer.`

## Objective

- Turn the open context menu into a real honeycomb composition by tightening the offset math, reducing dead air, and keeping submenu placement coherent around the denser root hive.

## Covered Inputs

- `N001` Hexagons should sit next to each other like a bee hive.
- `N007` The recomposition should save space and feel better organized.
- `N008` Existing shortcut-driven menu behavior must survive the layout change.

## Prerequisites

- `01-01-standard-ring-order-and-node-menu-contract` complete with passing proof.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\runtime\workbench\03-interaction-and-state.js`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\runtime\workbench\04-context-menu-and-composer.js`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\css\workbench\overlays\05-overlays-and-composer.css`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\runtime\workbench\03a-context-menu-shortcuts.js`

## Deliverables

- A tighter honeycomb offset model for root and relevant submenu layers.
- Updated root layer composition that visibly reads as a hive around the central core.
- Submenu origin and bounds logic tuned for the denser composition.
- Browser proof that the menu still opens, routes actions, and fits the host safely.

## Dependency Impact

- `03-03-visual-polish-and-responsive-tuning` depends on this phase because polish cannot rescue the layout if the geometry remains loose or broken.
- Weak proof here would make later screenshots meaningless because the underlying spatial contract would still be wrong.

## Validation Depth

- `Critical UI foundation`

## Implementation Steps

1. Refine the honeycomb coordinate generator so the spacing reflects actual hex size instead of loose radial-style padding.
2. Apply the honeycomb family to the root node-context layer and any submenu layers that benefit from the same composition.
3. Update bounds and submenu-origin calculations so the denser hive remains visible near host edges.
4. Confirm keyboard navigation, shortcut emphasis, and click execution still work with the new positions.
5. Capture large-screen browser proof before allowing polish work to proceed.

## Scope Exceptions

- Do not treat final color, shadow, or typography tuning as complete in this phase unless a visual defect directly blocks proof.

## Do Not Do

- Do not widen the runtime rewrite into unrelated canvas interaction logic.
- Do not accept screenshot-only proof without submenu-open or action-execution checks.

## Acceptance Checklist

- Root menu hexagons visually cluster as a hive instead of a loose orbit.
- First-ring positions respect the ordering contract from subbundle 01.
- Submenus still open cleanly and remain visible.
- Shortcut labels and focus states remain readable in the denser layout.

## Proof Required

- Browser proof on `/projects/{projectId}/structure` at `1600x1000` showing:
  - open node context menu
  - first-ring honeycomb composition
  - one submenu-open path or leaf execution path after the geometry change
- Screenshot artifact:
  - `output/playwright-mcp/hive-context-menu-desktop.png`
- Execution-report analytics row recording route, viewport, Playwright actions, assertions, screenshot path, and result.

## Browser Validation Logging

- Route: `/projects/{projectId}/structure`
- Viewport: `1600x1000` or maximized desktop work area
- Playwright MCP actions: open a representative node context menu, verify the first ring and surrounding hive in DOM or bounding-box terms, open one submenu or execute one leaf action, capture a screenshot
- Screenshot review questions:
  - Do the hexagons visually read as adjacent cells rather than separated floating buttons?
  - Is the first ring easy to scan clockwise?
  - Is anything clipping or colliding after the denser packing?

## Progression Gate

- Visual polish may continue only after browser proof shows a real honeycomb composition with clean submenu behavior.

## Suggested Agent Prompt

```text
Implement only subbundle 02 for the project-structure canvas hive context menu bundle.
Refine the root and submenu honeycomb geometry so the menu reads like a dense hive, keep the first-ring ordering contract intact, preserve shortcut and submenu behavior, and prove the result with large-screen browser evidence.
```
