# SB02-processes-lazy-loading

## Status

- `Completed`

## Objective

Make the Processes workspace load only the data required for the initially visible state, then explicitly load runtime, analytics, party, and option data when tabs or dialogs require it.

## Success Criteria

- `LoadWorkspaceAsync` no longer eagerly loads runtime-only options, party options, analytics, or improvements for hidden sections.
- Runtime and analytics tabs still load their required data when selected.
- Role/template dialogs still open with workflow/provider options loaded.
- Existing process workspace behavior is preserved.

## Covered Inputs

- `REQ-PROC-001`

## Prerequisites

- `SB01` complete.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.razor.cs`
- `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.Loading.cs`
- `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.DefinitionCrud.cs`
- `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.TemplateLibrary.cs`
- `repo://tests/CanDoItAll.Tests.Components/ProcessWorkspaceTests.cs`

## Deliverables

- Deferred-load flags and ensure methods for Processes workspace data.
- Tab/dialog boundaries that load data before rendering or executing dependent behavior.
- Component tests for initial-load and deferred-load behavior.

## Dependency Impact

- `SB05` final validation depends on this phase because the web app should start and navigate without hidden-section process work being triggered by default.

## Validation Depth

- `Critical UI foundation`

## Implementation Steps

1. Add explicit deferred-load state for runtime options, definition options, party options, analytics, and improvements.
2. Remove eager hidden-section calls from `LoadWorkspaceAsync`.
3. Load runtime and analytics data from tab-change or runtime-pane entry points.
4. Load role/template dialog dependencies immediately before those dialogs open.
5. Add or update component tests.

## Scope Exceptions

- No process storage schema or service API changes are planned.
- No broad cache layer is introduced.

## Do Not Do

- Do not silently swallow service failures.
- Do not pre-load template packs only to mask dialog latency.

## Acceptance Checklist

- Initial load keeps definition list and selected editor behavior.
- Analytics/improvements are fetched only when needed.
- Runtime options are fetched only when runtime sections need them.
- Tests prove deferred behavior.

## Proof Required

- Targeted component test command covering `ProcessWorkspaceTests`.
- Relevant build proof in `SB05`.

## Browser Validation Logging

- Target route: Processes workspace route during final web-app startup if available.
- Viewport passes: N/A unless layout changes are introduced.
- Playwright actions or assertions: N/A unless layout changes are introduced.
- Screenshot evidence: N/A unless layout changes are introduced.
- Review questions: confirm no visible UI layout changes were made.

## Progression Gate

- Processes component tests must pass before final validation.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Work outcome-first: preserve the listed scope boundaries, verify prerequisites before editing, make the smallest correct change set, capture the required proof, update the execution report rows, and stop if the progression gate cannot honestly pass.
```
