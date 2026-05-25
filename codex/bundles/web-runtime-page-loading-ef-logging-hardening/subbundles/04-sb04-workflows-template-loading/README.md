# SB04-workflows-template-loading

## Status

- `Completed`

## Objective

Remove ordinary page-init workflow template/catalog work and load workflow component/provider data only when the active tab or command needs it.

## Success Criteria

- `WorkflowsPage.OnInitializedAsync` does not call example catalog seeding.
- `LoadPageAsync` does not eagerly list all workflow components and provider options.
- Editor, templates, analytics, and starter workflow paths load the component library through an explicit gate.
- Component tests prove initial load and deferred tab behavior.

## Covered Inputs

- `REQ-WF-001`

## Prerequisites

- `SB01` complete.

## Exact Source References

- `repo://src/CanDoItAll.Modules.AgentFramework/Pages/WorkflowsPage.razor.cs`
- `repo://src/CanDoItAll.Modules.AgentFramework/Pages/WorkflowsPage.razor`
- `repo://src/CanDoItAll.Modules.AgentFramework/Catalog/WorkflowExampleCatalogSeedService.cs`
- `repo://tests/CanDoItAll.Tests.Components/WorkflowsPageTests.cs`

## Deliverables

- Lazy component/provider option load gate.
- Tab-change integration for sections requiring the component library.
- Tests for no initial seeding/catalog load and deferred component load.

## Dependency Impact

- `SB05` final validation depends on this phase because the workflow page must not pay template-pack costs on navigation.

## Validation Depth

- `Critical UI foundation`

## Implementation Steps

1. Remove page-init call to example catalog seeding.
2. Remove eager component/provider loading from page refresh.
3. Add an explicit component-library load gate.
4. Invoke the gate from editor/templates/analytics tab changes and starter workflow creation.
5. Add or update component tests.

## Scope Exceptions

- Background catalog warmup remains owned by existing hosted services.
- This phase does not remove example templates from the product.

## Do Not Do

- Do not load template packs during ordinary page initialization.
- Do not render editor/template sections with empty provider data after they are selected.

## Acceptance Checklist

- Initial page data still includes settings, definitions, selected definition, and runs.
- Component/provider collections load once on demand.
- Tests prove the deferred behavior.

## Proof Required

- Targeted component test command covering `WorkflowsPageTests`.
- Relevant build proof in `SB05`.

## Browser Validation Logging

- Target route: Workflows page during final web-app startup if available.
- Viewport passes: N/A unless layout changes are introduced.
- Playwright actions or assertions: N/A unless layout changes are introduced.
- Screenshot evidence: N/A unless layout changes are introduced.
- Review questions: confirm no layout-affecting markup changes were made.

## Progression Gate

- Workflows component tests must pass before final validation.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Work outcome-first: preserve the listed scope boundaries, verify prerequisites before editing, make the smallest correct change set, capture the required proof, update the execution report rows, and stop if the progression gate cannot honestly pass.
```
