# SB02: Upstream Artifact Materialization

## Status

- Status: `Completed`
- Critical foundation: `Yes`

## Scope

- Add generic orchestration for configured upstream artifact inputs that are missing at downstream dispatch time.
- Reopen downstream blocked dependents after upstream artifact materialization completes.

## Objective

Prevent downstream same-step retry loops for missing upstream artifacts by asking the producing agent-owned step to materialize the artifact.

## Covered Inputs

- `N002`
- `N003`
- `N004`
- `N005`

## Prerequisites

- SB01 complete.
- Artifact input resolution can identify source step runs.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Models.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactValidation.cs`
- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeProgressionPlanner.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`

## Dependency Impact

- Dispatch candidate shape now carries source step run metadata for artifact inputs.
- Runtime progression reopens only blocked dependents whose block reason explicitly names missing upstream artifacts.

## Validation Depth

- Targeted dispatch/progression tests.
- Full process dispatch test class.

## Implementation

- Dispatch artifact input records now carry source step run id, source step concurrency token, source step status, and whether the source has an agent executor.
- Before dispatching a downstream agent, the dispatcher detects missing configured upstream artifact inputs.
- If the producing source step is agent-owned and completed, blocked, or failed, the downstream step is blocked and the source step receives a targeted rerun directive.
- When an upstream step completes, blocked dependent steps whose block reason is explicitly missing upstream artifacts are reopened to `Ready` or `WaitingApproval`.

## Implementation Steps

- Extend artifact input model with source step metadata.
- Add downstream pre-dispatch missing-input detection.
- Use existing rerun infrastructure to request source materialization.
- Reopen blocked dependents after source completion.
- Add regression tests.

## Do Not Do

- Do not rerun downstream for missing upstream inputs.
- Do not reopen unrelated blocked steps.
- Do not create a product-specific process rule.

## Acceptance Checklist

- [x] Downstream missing upstream artifact block does not retry same step.
- [x] Source materialization path is generic and artifact-input driven.
- [x] Upstream completion reactivates blocked dependent.
- [x] Full dispatch test class passes.

## Proof Required

- `bundle://proof/SB02/transcripts/targeted-tests.txt`

## Browser Validation Logging

- Not applicable. No frontend browser surface changed.

## Progression Gate

- Passed after targeted and full dispatch tests.

## Suggested Agent Prompt

Use `bundle://shared-prompts/implementation-prompt.md`.

## Closure

- Existing no-retry downstream behavior remains covered.
- New progression behavior is covered by a regression test.
- The path is generic and artifact-contract driven.
