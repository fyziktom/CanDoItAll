# 02-sidebar-overflow-continuation-menu

## Status

- `Completed`

## Objective

- Replace desktop primary navigation internal scrolling with a final `more_up` continuation item that opens a dark floating panel of overflow page shortcuts.

## Covered Inputs

- `N003`: repair page height limitation and remove main menu internal scrolling.
- `N004`: overflow appears as final standard `more_up` menu item and opens on mouseover.
- `N005`: overflow pages render as small square cards with icon and one-word centered label, max three rows, expanding columns, dark menu background.
- `R003`, `R004`, `R005`, `R006`.

## Prerequisites

- Bundle preparation gate has passed.
- `01-01-tab-header-density` is complete or no shared CSS conflict remains.
- Imagegen planning reference has been reviewed as direction, not proof.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.Components\Components\AppShell.razor
- C:\repositories\CanDoItAll\src\CanDoItAll.SharedKernel\Navigation\ShellNavigationItem.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Web\Composition\ShellNavigation.cs
- C:\repositories\CanDoItAll\Tailwind\navigation\workbench-shell.css
- C:\repositories\CanDoItAll\codex\bundles\shell-tab-menu-density\evidence\continuation-menu-imagegen.png

## Deliverables

- Deterministic standard/overflow nav partitioning in `AppShell`.
- Final `more_up` standard nav control when overflow exists.
- Hover/focus fixed-position continuation panel using the dark sidebar background.
- Max-three-row expanding-column grid of compact square icon cards with one-word labels.
- Targeted component coverage for overflow rendering and mobile preservation.

## Dependency Impact

- This is the critical shell foundation. If it is wrong, routes can become inaccessible or the continuation panel can be clipped on every desktop page.
- Final browser proof must inspect the open panel before closure can proceed.

## Validation Depth

- `Critical UI foundation`.

## Implementation Steps

1. Add helper properties or methods to split desktop nav items into standard and overflow sets.
2. Keep active pages visible in the standard set when practical.
3. Render `more_up` as the last standard desktop nav item only when overflow exists.
4. Add continuation panel markup with role/menu semantics and one-word card labels.
5. Replace nav internal scroll styling with viewport-limited sidebar behavior and overflow-safe flyout styling.
6. Add targeted component tests for overflow rendering.

## Scope Exceptions

- Do not implement per-pixel JS measurement in this pass.
- Do not redesign mobile navigation; mobile should still show all navigation items directly.

## Do Not Do

- Do not remove existing navigation routes or badges.
- Do not make the overflow panel light themed.
- Do not allow the continuation panel to be clipped by the sidebar.
- Do not use a scrollable overflow menu as the solution.

## Acceptance Checklist

- Desktop nav has no `overflow-y-auto` primary menu behavior.
- Overflow items are still accessible.
- `more_up` renders as the final standard desktop menu item when needed.
- The panel opens on hover and focus.
- Panel cards are compact squares with centered icon and one-word label.
- The grid has no more than three rows and grows columns for additional items.
- Mobile navigation continues to render all items.

## Proof Required

- Targeted component test showing `more_up`, continuation panel items, and preserved mobile items.
- CSS review showing no primary nav internal scroll.
- Browser large desktop screenshot with the continuation panel open and readable.

## Browser Validation Logging

- Route: `/processes`.
- Viewport: desktop shell breakpoint at or above 1280px width.
- Actions/assertions: hover/focus the `more_up` control, assert panel visibility, inspect no clipping, inspect max-three-row card grid, inspect no internal sidebar nav scrollbar.
- Screenshot paths: record open-state screenshot in `reviews/01-execution-report.md`.
- Review questions: are all cards readable, compact, centered, unclipped, and dark themed?

## Progression Gate

- `02` passes only when component proof and browser open-state proof show that overflow navigation is accessible without a sidebar scrollbar.

## Suggested Agent Prompt

```text
Implement this subbundle only. Replace desktop sidebar internal nav scrolling with a more_up continuation item and a fixed dark overflow panel. Keep mobile navigation unchanged, preserve routes and badges, add targeted tests, capture open-state browser proof, and stop if any route becomes inaccessible.
```
