# Structured Input

## Core Objective

- Recompose the project-structure right-click menu into a tighter honeycomb so the hexagons visually share edges, the common actions sit in a stable first ring, and the menu uses less space while remaining readable and keyboard-friendly.

## Hard Constraints

- Use the reference image only for spatial inspiration, not for copying the game's visual skin.
- Preserve the shared workbench menu system rather than introducing a project-page-only fork.
- Keep the existing single-letter shortcut model, visible shortcut emphasis, submenu behavior, and accessible labeling working after the layout change.
- Standardize the most-used node actions in a clockwise first-ring order for all node context menus: `Blocks`, `Assets`, `Tasks`, `Progress`, `Markers`, then the best remaining node-specific action in the sixth slot.
- Remaining node-specific actions may move into the surrounding hive, but should stay grouped and discoverable.
- The change must save space or at minimum use space more intentionally than the current loose orbit.

## Source Artifacts

- `inputs/00-original-request.md`
- `inputs/01-source-artifacts.md`

## Input Coverage Signals

- `N001` Hexagons must sit next to each other like a bee hive, not in a loose radial spread.
- `N002` The game screenshot is composition inspiration only; the shipped visual style must remain CanDoItAll’s own.
- `N003` The center or first ring should stabilize the most-used actions.
- `N004` The clockwise first-ring order must be `Blocks`, `Assets`, `Tasks`, `Progress`, `Markers`, plus the best remaining node-specific slot.
- `N005` This standard composition applies to node context menus for all nodes.
- `N006` The rest of the menu should be organized in a node-appropriate way rather than arbitrary spillover.
- `N007` The overall recomposition must save space and look more organized and graphically nicer.
- `N008` Existing keyboard-shortcut orientation from the previous bundle must keep working after the layout shift.

## Dependency And Sequencing Signals

- The node-menu ordering contract must be stable before geometry work can prove the intended first ring.
- The honeycomb geometry and submenu placement must be correct before polish work can make meaningful visual judgements.
- Browser proof must validate both spacing and interaction, because the request is fundamentally about composition rather than only data ordering.

## Validation Expectations

- Focused automated proof for the reordered node-action catalog and create-action ordering where practical.
- Real browser proof on `/projects/{projectId}/structure` showing the open context menu in its new hive arrangement on at least one representative node.
- Evidence that first-ring positions are stable for node menus and that submenu flows still open cleanly without clipping.
- Large-screen and narrower-width screenshots reviewed for spacing, overlap, clipping, and visual coherence.
- Prepared-stage and completed-stage bundle validator passes.

## UI Validation Strategy

- Start with a maximized or `1600x1000` browser pass on the structure canvas and capture a baseline of the open node context menu after the new composition lands.
- Review whether hexagon edges visually read as a honeycomb, whether the first ring is immediately scannable, and whether the layout wastes less empty space than the current orbit.
- Follow with a `1280x800` pass to verify the hive still fits cleanly near canvas edges and open submenus do not create clipping or awkward collisions.

## Browser Validation Analytics

- `02-02-hive-geometry-and-submenu-packing` will log the node-menu route, the open-menu Playwright actions, the DOM checks for layer geometry, and the large-screen screenshot path.
- `03-03-visual-polish-and-responsive-tuning` will log the same route at desktop and narrower widths plus any submenu-open checks and responsive screenshots.
- `04-04-browser-proof-and-closure` will consolidate the final screenshots, menu-order observations, and gate decisions into the execution report.

## Working Assumptions

- “Tasks” maps to the existing `Work` or task-creation/context area in the menu model rather than requiring a brand-new action family.
- “For all nodes” refers to node context menus, not necessarily the empty-canvas root create menu, unless the runtime change naturally improves both.
- The current hex clip-path buttons can remain; the composition problem is primarily geometry, slot order, and spacing rather than inventing a new shape primitive.
- The best sixth first-ring slot may differ by node type, but it should be explicit and deterministic.

## Primary Risks

- A geometry-only tweak could still leave the menu visually loose if action ordering remains arbitrary.
- An order-only tweak could still fail the bee-hive request if the spacing math keeps large gaps between hexes.
- Submenu placement could regress and overlap the root hive or run off the host bounds once the root layer is more compact.
- Keyboard-driven flows might become visually confusing if the new positions do not preserve readable labels and focus states.
