# 03 Retry Routing And Upstream Artifact Recovery

## Status

- `Completed`

## Objective

Prevent retry loops against the wrong agent by classifying missing artifacts as current-step outputs or upstream input gaps before choosing recovery action.

## Covered Notes

- User noted that if a missing artifact belongs to a previous agent, retrying the current agent cannot fix it.
- Retrying five times without solving the missing artifact is unacceptable.

## Prerequisites

- Subbundle 01 failure classification is available.
- Existing manual rerun and run health read models from the previous bundle are present.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Automation\Dispatch\ProcessRunAutomationDispatchService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Automation\Dispatch\ProcessRunAutomationDispatchService.ArtifactValidation.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Runtime\ProcessesService.Runtime.Rerun.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Runtime\ProcessRuntimeViewModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Runtime\ProcessesService.RuntimeReadQuery.Support.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessRuntimeOperatorReadModelTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessRunAutomationDispatchServiceTests.cs`

## Scope

- Add ownership classification for artifact gaps.
- Current step missing outputs may retry current step.
- Upstream missing inputs must reopen/block upstream or surface an operator action; do not retry downstream blindly.

## Dependency Impact

- Unlocks deterministic mock failure matrix and simplified three-agent proof.
- If wrong, phase 05 can pass for the wrong reason.

## Validation Depth

- Integration tests for current-step missing artifact retry.
- Integration tests for upstream missing artifact routing.
- Read-model test for recovery classification display if the UI-facing model changes.

## Implementation Steps

1. Locate where missing required artifacts are resolved after execution.
2. Add a strongly typed classification for artifact-gap ownership if one does not already exist.
3. Build recovery directives that name the owning step and expected artifact.
4. For upstream gaps, avoid consuming attempts on the downstream step.
5. Add tests for both ownership paths.

## Scope Exceptions

- If full automatic upstream rerun is too risky, implement explicit blocked state with an operator action and document the follow-up.

## Do Not Do

- Do not delete prior attempts.
- Do not let downstream steps fabricate upstream artifacts.
- Do not retry indefinitely.

## Acceptance Checklist

- Missing current-step output retries current step or fails with current-step classification.
- Missing upstream input does not burn all downstream attempts.
- Recovery directive names the owning step/artifact.
- Tests cover both cases.

## Proof Required

- Focused integration tests.
- Narrow build for touched projects.
- Execution report updated.

## Browser Validation Logging

- Required only if operator UI/action state changes. Record route, viewport, assertions, and screenshots if changed.

## Progression Gate

- Proceed to subbundle 04 only after current-step and upstream artifact ownership tests are green or an explicit blocker is documented.
- Stop and repair this subbundle if upstream missing artifacts still consume downstream retry attempts.

## Suggested Agent Prompt

```text
Implement subbundle 03 only. Add artifact-gap ownership classification and prevent downstream retry loops when the missing artifact belongs to an upstream step.
```
