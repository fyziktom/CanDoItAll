# Module Navigation Contributions

## Status

- `Completed`

## Objective

- Add a generic module-owned shell navigation contribution path and use it to place AgentFramework `Workflows` immediately after `Agents`.

## Success Criteria

- A shared contract lets modules contribute additional shell navigation items tied to a parent route.
- Contributions carry subitem metadata and a short design note for the future nested menu design.
- AgentFramework registers a contributor for `Workflows` at `/agents/workflows`.
- The shell menu renders `Workflows` after `Agents` and before `Resources` when desktop space allows.
- Active-route matching can resolve `/agents/workflows` to the contributed `Workflows` item.

## Covered Inputs

- N002, N003, R003, R004, R005.

## Prerequisites

- Prepared-stage bundle validator passes.
- Confirm `/agents/workflows` route exists.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.SharedKernel\Navigation\ShellNavigationItem.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Composition\ShellNavigation.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Components\Layout\MainLayout.State.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Components\Layout\MainLayout.Workbench.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Services\AgentFrameworkModuleServiceCollectionExtensions.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\WorkflowsPage.razor`

## Deliverables

- Shared contributor contract.
- ShellNavigation merge and route-matching overloads.
- MainLayout injection and usage.
- AgentFramework contributor registration.
- Targeted tests proving merge order and active route.

## Dependency Impact

- Final closure and future module menu extensions depend on this being generic. A hardcoded Web-only item would fail the core request.

## Validation Depth

- Critical navigation composition foundation with targeted tests and browser-proof closure.

## Implementation Steps

1. Add shared navigation contribution records/interfaces.
2. Update Web shell navigation composition to merge contributors after parent routes.
3. Update MainLayout to pass injected contributors into item creation and active route matching.
4. Add AgentFramework contribution and service registration.
5. Add tests for order, active route, and subitem metadata.
6. Capture Playwright desktop menu-order proof.

## Scope Exceptions

- Visual nested-subitem styling is intentionally deferred; metadata and comments prepare it for a later design.

## Do Not Do

- Do not introduce indentation, nesting, accordions, or a new subitem visual treatment.
- Do not move the Workflows route or duplicate the page.

## Acceptance Checklist

- `ShellNavigation.GetItems(... contributors ...)` returns `Agents`, `Workflows`, `Resources`.
- `ShellNavigation.MatchRoute("agents/workflows", contributors)` returns `/agents/workflows`.
- Registered AgentFramework contributor marks `Workflows` as a subitem.
- Desktop screenshot shows the menu order.

## Proof Required

- Targeted navigation tests.
- Playwright MCP desktop route `/agents`.
- Screenshot `codex/bundles/shell-menu-navigation-contributions/evidence/agents-workflows-menu-order.png`.

## Browser Validation Logging

- Target route: `/agents`.
- Required viewport: desktop, `1440x900` or larger, expanded menu.
- Actions/assertions: read visible menu labels; assert `Agents`, `Workflows`, `Resources` order.
- Screenshot: `evidence/agents-workflows-menu-order.png`.
- Review question: `Workflows` should look like the same standard menu row style used by surrounding items.

## Progression Gate

- Tests and Playwright order proof pass, and the subitem metadata note exists in code.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Work outcome-first: preserve the listed scope boundaries, verify prerequisites before editing, make the smallest correct change set, capture the required proof, update the execution report rows, and stop if the progression gate cannot honestly pass.
```
