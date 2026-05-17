# Structured Input

## Core Objective

- Make the shared shell denser and safer at desktop height: large screens keep tab controls and status badges on one row, and the sidebar uses a continuation flyout instead of an internal scrollbar.

## Success Criteria

- At the desktop shell breakpoint (`xl` / 1280px and wider), inline tabs, tab search, tab overflow/reopen controls, and top-bar badges align on one row.
- The tab summary/actions do not add avoidable height on large desktop focus workbench routes.
- The primary sidebar does not use `overflow-y-auto` for navigation.
- When navigation items exceed the visible standard menu budget, a final standard item with `more_up` appears.
- Hovering or focusing `more_up` opens a floating dark continuation panel.
- Overflow pages in that panel are small square icon cards with centered icons and one-word labels.
- The continuation panel grid uses no more than three rows and adds columns when more items are present.

## Hard Constraints

- Apply the tab-row compaction only to large desktop so smaller layouts keep conservative stacking.
- Preserve existing navigation routes, badges, active-state matching, and mobile menu behavior.
- Use existing BaseLib layout primitives where practical; custom CSS is allowed for shell-specific overflow positioning and icon-card grid behavior.
- Generated image design is a planning aid only, not shipped UI proof.

## Allowed Side Effects

- Shared shell and tab strip markup may change.
- Tailwind source and compiled BaseLib output CSS may change.
- Targeted component tests may be added or updated.
- No module-specific page behavior should change beyond receiving the shell layout.

## Source Artifacts

- `inputs/00-original-request.md`
- Conversation screenshot of current shell layout.
- `evidence/continuation-menu-imagegen.png`

## Input Coverage Signals

- `N001`: tab search must move to the same row as tabs at the row end on large desktop.
- `N002`: badges/stats about tabs must move to the same row as tabs on large desktop.
- `N003`: sidebar page-height limitation and internal scroll must be repaired.
- `N004`: overflow must become a final standard `more_up` menu item with a hover-open floating continuation panel.
- `N005`: continuation panel items must be small square icon cards with centered icon and one-word label, max three rows, expanding columns, and dark menu background.

## Dependency And Sequencing Signals

- Tab-row compaction can land before sidebar work and is independent except for shared shell CSS.
- Sidebar continuation is the critical foundation because it changes navigation rendering and affects all desktop routes.
- Browser proof must validate both a height-locked focus route and at least one standard route with the sidebar visible.

## Validation Expectations

- Run targeted component tests covering tab strip/search layout markers and sidebar overflow rendering.
- Rebuild Tailwind output CSS after changing Tailwind source.
- Run a solution or test-project build/test command that exercises the affected components.
- Use a large desktop browser viewport to inspect tab row alignment and the open continuation panel.
- Use a narrower desktop/tablet-width pass to confirm smaller layouts did not inherit the desktop-only compaction.

## Evidence Contract

- `python codex\skills\bundles\candoitall-bundle-preparation\scripts\validate_bundle.py codex\bundles\shell-tab-menu-density --profile feedback --stage prepared`
- `npm --prefix Tailwind run build`
- Targeted component tests for `AppTabStrip` and `AppShell`.
- Browser screenshot evidence for `/processes` at large desktop with the continuation panel open.
- Browser screenshot or DOM check for a narrower viewport proving the layout still stacks safely.
- `python codex\skills\bundles\candoitall-bundle-preparation\scripts\validate_bundle.py codex\bundles\shell-tab-menu-density --profile feedback --stage completed`

## UI Validation Strategy

- First pass: `/processes` or another focus workbench route at large desktop. Confirm one-row tab/status chrome, no sidebar internal scrollbar, visible `more_up`, and open continuation panel.
- Open-state questions: is the whole panel readable, unclipped by the sidebar or viewport, above page chrome, and using the same dark menu background?
- Density questions: are continuation cards genuinely small square cards, with one centered icon and one centered one-word label?
- Follow-up pass: narrower width below the large desktop breakpoint to confirm the tab controls may stack and the sidebar/mobile behavior remains conservative.

## Browser Validation Analytics

- Record route, viewport, hover/click/focus actions, DOM assertions, screenshot paths, and pass/fail result in `reviews/01-execution-report.md`.
- Required rows: `01-01-tab-header-density`, `02-02-sidebar-overflow-continuation-menu`, and `03-03-validation-and-closure`.

## Working Assumptions

- "Badges with stats about tabs" refers to `MainLayoutTopBar` status badges including live items and tab count.
- The request is for desktop shell behavior; mobile navigation should not be redesigned.
- A fixed count-based overflow budget is acceptable unless browser proof shows the sidebar still overflows on the target large desktop viewport.

## Primary Risks

- The sidebar flyout can be clipped if positioned inside an overflow-hidden ancestor, so it should follow the database flyout pattern with fixed positioning.
- Changing shell height behavior can clip standard pages if body overflow is not handled carefully.
- Active navigation state can become unclear if the active page is moved into overflow, so the renderer must account for active overflow routes.
