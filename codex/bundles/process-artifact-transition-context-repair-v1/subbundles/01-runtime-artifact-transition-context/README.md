# SB01 Runtime Artifact Transition Context

## Status

- `Completed`

## Objective

- Repair the double-validation artifact failure by forwarding process-owned completion lineage into transition-time required-artifact validation.

## Covered Inputs

- `bundle://inputs/00-original-request.md`
- `bundle://inputs/01-source-artifacts.md`
- `repo://codex/bundles/process-run-first-step-artifact-binding-failure-inputs-v1/inputs/03-api-evidence-index.md`

## Prerequisites

- Fresh failed-run evidence identifies `StaleOrWrongRun` after a current-run workspace write.
- Source inspection confirms `TransitionStepAsync` revalidates as manual without automation lineage.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs`
- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.StepTransitions.cs`
- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeViewModels.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs`

## Deliverables

- Internal transition request context for artifact validation lineage.
- Finalizer result/request forwarding for direct agent, workflow, subprocess, and manager recovery paths.
- Focused integration test coverage for automation pass and manual stale-lineage rejection.
- Critical proof manifest under `proof/SB01/manifest.md`.

## Dependency Impact

- Downstream process automation can complete required artifact-producing steps when lineage matches the actual executor context.
- Manual/API transition callers remain on the no-context validation path.

## Validation Depth

- Failing-first test for the old double-validation failure.
- Passing targeted integration tests.
- Source assertions and anti-stub audit over changed runtime files.
- Changed-file hash proof.

## Implementation Steps

1. Add internal artifact validation context to `ProcessStepTransitionRequest`.
2. Capture the last finalizer validation context in `ProcessStepCompletionFinalizerResult`.
3. Forward that context into `TransitionStepAsync`.
4. Use the request context when transition-time artifact validation runs.
5. Add integration tests.

## Do Not Do

- Do not remove transition-time artifact validation.
- Do not mark all manual artifacts as current.
- Do not add public API fields for internal automation lineage.

## Acceptance Checklist

- Matching direct-agent workspace-write lineage can complete through `TransitionStepAsync`.
- Stale manual workspace-write lineage remains rejected.
- Required content validation remains active.
- Proof manifest and semantic invariants exist.

## Proof Required

- `bundle://proof/SB01/transcripts/failing-first.txt`
- `bundle://proof/SB01/transcripts/passing.txt`
- `bundle://proof/SB01/transcripts/source-assertions.txt`
- `bundle://proof/SB01/transcripts/anti-stub-audit.txt`
- `bundle://proof/SB01/transcripts/changed-file-hashes.txt`

## Browser Validation Logging

- N/A for SB01; this is process runtime service behavior validated by integration tests.

## Progression Gate

- Passed. SB01 tests pass and `proof/SB01/manifest.md` cites existing transcript files.

## Suggested Agent Prompt

```text
Implement SB01 exactly: carry process-owned artifact validation context into transition validation, keep manual stale-artifact rejection, and prove both with focused integration tests.
```
