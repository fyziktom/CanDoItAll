# 03-visual-polish-and-responsive-tuning

## Status

- `Ready`

## Objective

- Tune the denser hive so it remains readable, visually coherent, and edge-safe across desktop and narrower widths without losing the compact spatial win.

## Covered Inputs

- `N002` Use the reference image only as layout inspiration, not as a copied skin.
- `N006` Remaining items should still be organized in a node-appropriate way.
- `N007` The recomposition should save space and look better organized and graphically nicer.

## Prerequisites

- `02-02-hive-geometry-and-submenu-packing` complete with browser proof.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\css\workbench\overlays\05-overlays-and-composer.css`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\css\workbench\responsive\06-motion-and-responsive.css`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\runtime\workbench\03-interaction-and-state.js`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\runtime\workbench\04-context-menu-and-composer.js`

## Deliverables

- Final sizing, spacing, and label-fit tuning for the hive.
- Responsive or edge-safe adjustments that preserve readability when the menu opens near tighter host bounds.
- Browser screenshots at large and narrower widths that show the final composition clearly.

## Dependency Impact

- `04-04-browser-proof-and-closure` depends on this phase because final closure needs reviewed screenshots that actually satisfy the visual complaint.
- Weak proof here would leave the central design goal unresolved even if the raw geometry technically changed.

## Validation Depth

- `UI, browser-proof, and screenshot review`

## Implementation Steps

1. Tune hex dimensions, label fit, and ring spacing so the hive is compact without collapsing readability.
2. Adjust hover, focus, and layering polish only where needed to support the new composition.
3. Validate the open menu at desktop and narrower widths near realistic canvas positions.
4. Capture screenshots and answer the visual review questions explicitly in the execution report.

## Scope Exceptions

- Do not introduce a theme overhaul, game-like materials, or unrelated art direction changes.

## Do Not Do

- Do not copy the reference image’s yellow neon style or weapon-wheel identity.
- Do not reopen ordering logic unless browser proof shows the first ring itself is wrong.

## Acceptance Checklist

- The menu feels denser and better organized than the current orbit.
- Labels remain readable without obvious clipping.
- Open submenus and the root hive coexist without awkward overlap.
- The final visuals remain coherent with CanDoItAll’s existing workbench style.

## Proof Required

- Browser screenshots on `/projects/{projectId}/structure` at:
  - `1600x1000` => `output/playwright-mcp/hive-context-menu-desktop.png`
  - `1280x800` => `output/playwright-mcp/hive-context-menu-narrow.png`
- If submenu layout changes materially, add:
  - `output/playwright-mcp/hive-context-submenu-narrow.png`
- Execution-report analytics rows documenting the screenshot review outcomes.

## Browser Validation Logging

- Route: `/projects/{projectId}/structure`
- Viewports: `1600x1000` first, then `1280x800`
- Playwright MCP actions: open the node context menu, optionally open one submenu, capture screenshots, inspect layout density and label readability
- Screenshot review questions:
  - Are the hex edges visually close enough to read as a hive?
  - Is the menu using space intentionally instead of leaving awkward holes?
  - Are labels, shortcut underlines, and icons still readable?
  - Does the screen remain stylistically coherent with the app?

## Progression Gate

- Closure may continue only after the screenshot review says the layout complaint is genuinely solved rather than merely changed.

## Suggested Agent Prompt

```text
Implement only subbundle 03 for the project-structure canvas hive context menu bundle.
Polish the hive spacing, label readability, and responsive behavior so the final menu looks denser and better organized without copying the reference image’s style, then capture desktop and narrow browser screenshots to prove it.
```
