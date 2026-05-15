# 03-plugins-ui-package-install-and-restart

## Status

- `Completed`

## Objective

Extend `/plugins` so users can add plugins from the configured plugin catalogue, upload a plugin zip, see restart-required state, and request a graceful app restart.

## Covered Inputs

- `N003`, `N005`, `N006`, `N009`, `N010`
- Requirements: `R012`, `R013`, `R014`

## Prerequisites

- SB02 package services and API are complete.
- Package install result and restart status are available from DI.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Plugins\Pages\PluginsPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\PluginsApi.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\PluginsPageTests.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\CanDoItAll.Components.BaseLib.csproj`

## Deliverables

- `/plugins` package acquisition section using existing component library patterns.
- Catalogue package list with install button.
- Upload zip control with validation feedback.
- Restart-required alert/banner with restart button.
- Component tests for package controls and restart request.
- Browser proof for the final UI state.

## Dependency Impact

- SB04 depends on this proof to close UI raw notes.
- Weak UI proof leaves `N006`, `N009`, and `N010` unresolved.

## Validation Depth

- `Critical UI foundation`

## Implementation Steps

1. Query CanDoItAll component MCP before changing layout.
2. Add package catalogue and upload state to `PluginsPage`.
3. Add package install handlers using SB02 services.
4. Add restart-required status and restart request handler.
5. Add component tests.
6. Run browser proof on `/plugins` with package controls visible and restart-required state.

## Scope Exceptions

- Rich icon rendering may be limited to metadata retention if no existing image component pattern is necessary.

## Do Not Do

- Do not build one-off layout wrappers when shared components cover the structure.
- Do not hide package install failures behind generic messages.
- Do not require users to know process ids or Task Manager steps.

## Acceptance Checklist

- Catalogue install affordance is visible.
- Upload zip affordance is visible.
- Restart-required state is visible after a package install requiring restart.
- Restart action calls backend restart service.
- Existing plugin detail tabs still work.

## Proof Required

- Component tests for package controls and restart flow.
- Browser proof on `/plugins`, large desktop viewport, with screenshot saved under bundle evidence or reviews artifacts.
- Narrower viewport pass if the new layout wraps or changes responsive behavior.
- Execution report SB03 gate and browser analytics rows updated.

## Browser Validation Logging

- Route: `/plugins`
- Viewports: large desktop first; narrower width if layout changes or wrapping is introduced.
- Actions/assertions: navigate to `/plugins`, verify package catalogue section, verify upload control, trigger/observe restart-required state when feasible, verify restart action is visible and not clipped.
- Screenshot paths: record under `codex/bundles/plugin-runtime-package-install/reviews/artifacts/`.
- Review questions: Are controls readable? Is hierarchy clear? Are install and restart actions distinguishable? Does text fit at desktop and narrower width? Are alerts not obscuring plugin details?

## Progression Gate

- SB04 may start only after component tests and browser proof show the package add and restart path.

## Suggested Agent Prompt

```text
Implement SB03 only. Use existing shared components on /plugins, add catalogue install, upload zip, restart-required state, restart action, component tests, browser proof, and execution report rows.
```
