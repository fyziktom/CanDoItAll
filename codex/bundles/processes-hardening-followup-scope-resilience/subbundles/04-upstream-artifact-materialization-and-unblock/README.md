# Upstream Artifact Materialization And Unblock

## Status

- Completed

## Objective

Prevent downstream steps from remaining blocked after upstream artifact recovery/materialization succeeds.

## Covered Inputs

- Original notes: see `bundle://inputs/02-structured-input.md`
- Requirements: RQ06, RQ09, RQ12

## Prerequisites

- Complete prerequisite subbundles according to `bundle://plan/01-phase-plan.md`.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Execution.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExecutionPrompt.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjection.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`

## Deliverables

- Record missing upstream artifact dependency with durable fingerprint.
- Request source step rerun/recovery without losing downstream resume intent.
- After source artifact is recorded, re-evaluate blocked/waiting downstream dependents.
- Move satisfied downstream step back to Ready/WaitingApproval or queue dispatch.

## Dependency Impact

- Critical. Fixes a likely permanent-stall path.

## Validation Depth

Critical semantic validation required.

Every completed subbundle must produce:

- failing-first or pre-change source/behavior proof,
- passing proof,
- source assertions,
- anti-stub audit,
- changed-file hashes,
- semantic invariant file,
- proof manifest update.

## Implementation Steps

1. Record missing upstream artifact dependency with durable fingerprint.
2. Request source step rerun/recovery without losing downstream resume intent.
3. After source artifact is recorded, re-evaluate blocked/waiting downstream dependents.
4. Move satisfied downstream step back to Ready/WaitingApproval or queue dispatch.
5. Prevent repeated source reruns with same materialization fingerprint.

## Scope Exceptions

None. Keep scope generic and PostgreSQL-only.

## Do Not Do

- Do not hardcode Blazor/.NET behavior into generic process runtime.
- Do not mix workflow internal status with process step completion.
- Do not add SQLite migrations or provider-switching logic.
- Do not close from prompt-only changes.
- Do not satisfy required artifacts with diagnostic placeholders.

## Acceptance Checklist

- [x] Record missing upstream artifact dependency with durable fingerprint.
- [x] Request source step rerun/recovery without losing downstream resume intent.
- [x] After source artifact is recorded, re-evaluate blocked/waiting downstream dependents.
- [x] Move satisfied downstream step back to Ready/WaitingApproval or queue dispatch.
- [x] Prevent repeated source reruns with same materialization fingerprint.

## Proof Required

- `bundle://proof/SB04/manifest.md`
- `bundle://proof/SB04/semantic-invariants.md`
- `bundle://proof/SB04/transcripts/failing-first.txt`
- `bundle://proof/SB04/transcripts/passing.txt`
- `bundle://proof/SB04/transcripts/source-assertions.txt`
- `bundle://proof/SB04/transcripts/anti-stub-audit.txt`
- `bundle://proof/SB04/transcripts/changed-file-hashes.txt`

## Browser Validation Logging

- N/A unless this subbundle changes browser-visible process flows or SB08 runs browser proof scenarios. If browser proof is used, record route, viewport, actions, screenshots, console, and result in `reviews/01-execution-report.md`.

## Progression Gate

- Do not start downstream dependent subbundles until this subbundle's proof manifest is complete and the targeted tests pass.

## Suggested Agent Prompt

Implement `Upstream Artifact Materialization And Unblock` from `codex/bundles/processes-hardening-followup-scope-resilience`. Preserve generic process semantics, keep workflows subordinate to processes, and update proof files before marking the subbundle complete.




