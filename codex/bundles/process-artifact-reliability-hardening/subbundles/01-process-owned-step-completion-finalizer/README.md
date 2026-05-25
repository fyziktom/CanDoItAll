# SB01 - Process-Owned Step Completion Finalizer

## Status

Ready

## Objective

Create a single process-owned finalization path used by every process executor kind before process step transition. This includes direct AgentFramework agents and workflow-backed roles. Do not move process artifact semantics into the workflow module.

## Covered Inputs

- N001, N004, N005, N006
- Findings F001, F003

## Prerequisites

- Current branch is `development`.
- PostgreSQL-only runtime assumption is accepted.
- Read `requirements/02-process-vs-workflow-boundary.md` before coding.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjection.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.CompletionArtifactRecovery.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Models.cs`
- `repo://src/CanDoItAll.Modules.Processes/Services/ProcessesService.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`

## Deliverables

- A process-owned finalizer service or equivalent refactor.
- Executor-neutral completion context model.
- Direct agent path routed through finalizer.
- Workflow-backed role path routed through finalizer.
- No process artifact validation code moved into workflow module.
- Replacement of implicit shared-mutable-candidate completion state with explicit finalizer result or ledger reload at finalization boundary.

## Dependency Impact

Critical foundation. SB02-SB06 must not start until this subbundle proves that all process executor outcomes flow through the same finalizer.

## Validation Depth

Deep semantic proof required. Add failing-first tests for workflow-backed role completion bypassing required artifact recovery/validation, then make them pass.

## Implementation Steps

1. Locate all step transition paths in `ProcessRunAutomationDispatchService.Dispatch.cs`.
2. Extract finalization logic currently used after direct `ExecuteUntilSettledAsync`.
3. Create `ProcessStepCompletionFinalizer` or equivalent owned by Processes.
4. Define executor-neutral context and result types.
5. Route direct AgentFramework execution through the finalizer.
6. Route workflow-handled outcomes through the finalizer.
7. Ensure finalizer returns the transition request/reason/status rather than letting executor-specific paths transition directly.
8. Ensure finalizer reloads the artifact ledger from PostgreSQL or consumes explicit projection results; do not rely on shared mutable `HashSet` state.
9. Keep workflows executor-side only. They may provide execution/workflow run evidence; they must not own process artifact contract satisfaction.

## Scope Exceptions

- Do not redesign the workflow runtime itself.
- Do not create a new workflow artifact contract system.

## Do Not Do

- Do not add SQLite logic.
- Do not move `ProcessArtifactExpectation` validation into Agents workflows.
- Do not close the subbundle with only direct-agent tests.
- Do not leave `HandleWorkflowExecutionOutcomeAsync` as a transition-only bypass.

## Acceptance Checklist

- [ ] Direct AgentFramework completion calls the finalizer.
- [ ] Workflow-backed role completion calls the same finalizer.
- [ ] Finalizer owns artifact projection/validation/recovery decision sequencing.
- [ ] Step transition happens after finalizer result.
- [ ] Tests prove workflow-backed role cannot complete while required artifacts remain missing.
- [ ] Source assertions show no duplicated process artifact semantics in workflow code.

## Proof Required

- `proof/SB01/manifest.md`
- `proof/SB01/semantic-invariants.md`
- failing-first transcript for workflow-backed bypass
- passing transcript after finalizer routing
- source assertion transcript showing finalizer call sites
- changed-file hashes

## Progression Gate

Do not start SB02 until SB01 proof shows all executor kinds enter the process-owned finalizer.

## Browser Validation Logging

N/A unless this subbundle adds or changes browser-visible UI. If browser proof is needed for a process scenario, record route, viewport, actions, assertions, screenshots, and result in `reviews/01-execution-report.md`.

## Suggested Agent Prompt

Use the shared implementation prompt at `bundle://shared-prompts/implementation-prompt.md`, then append this subbundle README and the exact source references above. Execute only this subbundle. Record proof before moving on.
