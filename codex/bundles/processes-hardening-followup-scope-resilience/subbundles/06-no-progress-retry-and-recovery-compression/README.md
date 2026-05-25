# No-Progress Retry And Recovery Compression

## Status

- Completed

## Objective

Stop repeated identical attempts before max retry count when no new evidence or mutation occurs.

## Covered Inputs

- Original notes: see `bundle://inputs/02-structured-input.md`
- Requirements: RQ09, RQ11, RQ12

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

- Create no-progress fingerprints for missing tools, failed validation, scope violations, missing artifacts, and upstream input failures.
- Track per-step attempt fingerprints in journal or runtime diagnostics.
- Retry only if a new mutation, new evidence, provider repair, manager directive, or changed input appears.
- Route repeated no-progress to manager recovery, branch, or blocked/escalation according to disposition router.

## Dependency Impact

- Reliability layer after artifact and disposition fixes.

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

1. Create no-progress fingerprints for missing tools, failed validation, scope violations, missing artifacts, and upstream input failures.
2. Track per-step attempt fingerprints in journal or runtime diagnostics.
3. Retry only if a new mutation, new evidence, provider repair, manager directive, or changed input appears.
4. Route repeated no-progress to manager recovery, branch, or blocked/escalation according to disposition router.
5. Add tests that same failure does not repeat 5 times.

## Scope Exceptions

None. Keep scope generic and PostgreSQL-only.

## Do Not Do

- Do not hardcode Blazor/.NET behavior into generic process runtime.
- Do not mix workflow internal status with process step completion.
- Do not add SQLite migrations or provider-switching logic.
- Do not close from prompt-only changes.
- Do not satisfy required artifacts with diagnostic placeholders.

## Acceptance Checklist

- [x] Create no-progress fingerprints for missing tools, failed validation, scope violations, missing artifacts, and upstream input failures.
- [x] Track per-step attempt fingerprints in journal or runtime diagnostics.
- [x] Retry only if a new mutation, new evidence, provider repair, manager directive, or changed input appears.
- [x] Route repeated no-progress to manager recovery, branch, or blocked/escalation according to disposition router.
- [x] Add tests that same failure does not repeat 5 times.

## Proof Required

- `bundle://proof/SB06/manifest.md`
- `bundle://proof/SB06/semantic-invariants.md`
- `bundle://proof/SB06/transcripts/failing-first.txt`
- `bundle://proof/SB06/transcripts/passing.txt`
- `bundle://proof/SB06/transcripts/source-assertions.txt`
- `bundle://proof/SB06/transcripts/anti-stub-audit.txt`
- `bundle://proof/SB06/transcripts/changed-file-hashes.txt`

## Browser Validation Logging

- N/A unless this subbundle changes browser-visible process flows or SB08 runs browser proof scenarios. If browser proof is used, record route, viewport, actions, screenshots, console, and result in `reviews/01-execution-report.md`.

## Progression Gate

- Do not start downstream dependent subbundles until this subbundle's proof manifest is complete and the targeted tests pass.

## Suggested Agent Prompt

Implement `No-Progress Retry And Recovery Compression` from `codex/bundles/processes-hardening-followup-scope-resilience`. Preserve generic process semantics, keep workflows subordinate to processes, and update proof files before marking the subbundle complete.




