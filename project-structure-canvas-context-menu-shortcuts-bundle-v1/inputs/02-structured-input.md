# Structured Input

## Core Objective

- Make the right-click menu usable from the keyboard alone once the menu is open, using single-letter shortcuts that can open nested layers and choose leaves without relying on pointer hover.

## Hard Constraints

- Preserve the architect-specified shortcut letters where the request named an explicit key.
- Do not break existing canvas-global shortcuts such as zoom, help, diagnostics, minimap, clipboard, note composer shortcuts, or `Escape`.
- Do not hijack keyboard input while a text input, textarea, or other editable control has focus.
- Keep the implementation aligned with the shared `CanvasWorkbench` action model and runtime instead of a project-page-only fork.

## Source Artifacts

- `inputs/00-original-request.md`
- `inputs/01-source-artifacts.md`

## Input Coverage Signals

- `N001` keyboard-only orientation must improve
- `N002` nested submenu navigation must work from the keyboard
- `N003` block shortcut seeds must be preserved
- `N004` asset shortcut seeds must be preserved
- `N005` marker shortcut seeds must be preserved
- `N006` people, infrastructure, note, meetings, and work shortcut seeds must be preserved
- `N007` help modal must include shortcut guidance and become more browsable
- `N008` visible menu text should underscore the shortcut letter
- `N009` other right-menu options also need shortcuts
- `N010` `03-interaction-and-state.js` should be split logically if possible

## Dependency And Sequencing Signals

- The shared shortcut contract must land before runtime keyboard routing or help-modal copy can be trusted.
- The runtime interaction behavior must be stable before the help modal is rewritten around it.
- Final browser proof depends on both the keyboard behavior and the help-modal information architecture.

## Validation Expectations

- Focused component coverage for the shortcut contract and project-structure action catalog.
- Focused component coverage for help-modal structure and shortcut documentation.
- Real browser proof on `/projects/{projectId}/structure` that opens the context menu, drives nested menus by keyboard, verifies visible shortcut affordances, and captures screenshots of the help overlay in the open state.
- Prepared-stage and completed-stage validator passes before the bundle is declared ready or complete.

## UI Validation Strategy

- Start with a large-screen Playwright pass at `1600x1000` to validate root menus, nested submenus, underlined shortcut letters, and the open help overlay.
- Follow with a narrower pass at `1280x800` for the help overlay and any menu layout affected by the new shortcut affordance.
- Review screenshots for clipping, lateral overflow, readability, and coherence with existing workbench chrome.

## Browser Validation Analytics

- Subbundle `02-runtime-keyboard-navigation-and-menu-affordances` will log route, large-screen viewport, keyboard actions, and open-menu screenshots.
- Subbundle `03-help-modal-information-architecture-and-shortcut-docs` will log route, desktop and narrower viewports, page-switching actions, and open-help screenshots.
- Subbundle `04-browser-proof-and-closure` will consolidate the final keyboard-flow regression, screenshot paths, and pass or fail result in the execution report.

## Working Assumptions

- The requested behavior is intended for the shared workbench runtime as exercised on the project-structure route.
- Unlisted sibling actions should still receive shortcuts through a deterministic collision-safe fallback rule instead of requiring a hand-written map for every current and future leaf.
- A focused runtime extraction is an acceptable response to the maintainability request if a larger file split would create avoidable load-order risk.

## Primary Risks

- Shortcut collisions across large sibling sets such as block types and node context actions.
- Runtime load-order or export regressions if the shortcut refactor introduces a new module.
- Help-modal content drifting from the actual rendered shortcut contract.
- Weak browser proof that validates only closed-state triggers instead of the open overlay and open submenu states.
