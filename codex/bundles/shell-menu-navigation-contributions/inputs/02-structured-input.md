# Structured Input

## Core Objective

- Add delayed tooltip behavior to remaining shell menu tooltips and let modules contribute selected subpages to the main shell menu.

## Success Criteria

- Standard shell menu tooltips wait a few seconds before display.
- Popup menu trigger tooltips remain removed for `More`, `Opened`, and `Switch Database`.
- A generic module contribution contract exists outside the Web shell.
- AgentFramework contributes `Workflows` immediately after `Agents`.
- The contribution marks `Workflows` as a subitem while rendering it as a normal item for now.

## Hard Constraints

- Do not add a visual nested/subitem design in this bundle.
- Do not hardcode `Workflows` only in the static Web navigation list.
- Use Playwright MCP desktop proof for visible menu order and tooltip timing.

## Allowed Side Effects

- Shared-kernel navigation model may gain a contribution contract.
- MainLayout may inject and pass navigation contributors.
- AgentFramework module registration may add a navigation contributor service.

## Source Artifacts

- `inputs/00-original-request.md`
- `inputs/01-source-artifacts.md`

## Input Coverage Signals

- N001: all remaining menu tooltips need delayed popup behavior.
- N002: modules need a generic way to add extra main menu entries.
- N003: contributed subpages need subitem metadata for future visual design while currently rendering flat.

## Dependency And Sequencing Signals

- Tooltip delay can be implemented independently from navigation contributions.
- Navigation contribution proof depends on the existing `/agents/workflows` route.
- Final closure depends on both source tests and browser-visible proof.

## Validation Expectations

- Targeted tests for navigation contribution merge and route matching.
- Browser proof for tooltip timing and menu label order.
- Bundle validators at prepared and completed stages.

## Evidence Contract

- Targeted `dotnet test` result.
- Playwright MCP assertions and screenshots in `evidence/`.
- Updated execution report and raw-note closure table.

## UI Validation Strategy

- Use a desktop viewport large enough to show the expanded menu and the `Agents`, `Workflows`, `Resources` sequence.
- Hover a standard menu item and confirm tooltip absent before the delay and visible after the delay.
- No smaller-screen validation is required for this request.

## Browser Validation Analytics

- `01-tooltip-delay-coverage`: route `/agents`, desktop viewport, hover timing assertions, `evidence/menu-tooltip-delayed.png`.
- `02-module-navigation-contributions`: route `/agents`, desktop viewport, menu-order assertion, `evidence/agents-workflows-menu-order.png`.

## Working Assumptions

- The existing two-second floating-card delay satisfies "few seconds".
- `/agents/workflows` is the desired route because the Agents page already navigates to it.
- Shared kernel is the correct abstraction boundary for module-owned menu contribution metadata.

## Primary Risks

- Active route matching must include contributed items.
- DI must tolerate zero contributors and preserve current shell behavior.
- The menu capacity can place `Workflows` into `More` on short viewports; desktop proof must use a tall enough viewport.
