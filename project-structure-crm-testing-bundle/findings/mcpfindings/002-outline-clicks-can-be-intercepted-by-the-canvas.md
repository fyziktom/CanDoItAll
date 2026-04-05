# MCP Finding 002: Outline Clicks Can Be Intercepted By The Canvas Layer

## What Happened

- The outline index remained visible on the right side of the maximized canvas.
- A normal Playwright pointer click on the visible B04 tree row timed out because the canvas layer intercepted pointer events.
- I had to fall back to a programmatic DOM click to keep the test moving.

## Evidence

- Live Playwright error: pointer click on `.cda-treeview__row` was intercepted by `.cw-workbench__canvas-stack`.
- The rest of the authoring surface remained live, so this was not a dead page or stale route.

## Why This Matters

- The outline is the fastest recovery surface when the canvas is dense.
- If it cannot be clicked reliably, users lose the safest way to navigate imported plans and recover from bad camera state.

## Recommendation

- Fix the stacking or pointer-events contract between the canvas stack and the outline surface.
- Add a regression test that verifies outline row clicks still work while the canvas is maximized and populated.
