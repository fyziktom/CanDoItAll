# Disposition Routing Instead Of Hard Blocking

## Status

- Completed

## Objective

Route negative findings to modeled process branches instead of blocking whenever a governed disposition can be made.

## Covered Inputs

- Original notes: see `bundle://inputs/02-structured-input.md`
- Requirements: RQ05, RQ11, RQ12

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

- Add `ProcessDispositionRouter` or equivalent function after finalizer validation.
- Map artifact/proof failures to branch outcomes when branch definitions represent repair/rework/no-go/escalation.
- Keep `Blocked` for missing input, authority, environment, unsafe target, or no valid branch.
- Add branch tag inference and definition lint warning for ambiguous branch names.

## Dependency Impact

- Critical. Reduces unnecessary process stops.

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

1. Add `ProcessDispositionRouter` or equivalent function after finalizer validation.
2. Map artifact/proof failures to branch outcomes when branch definitions represent repair/rework/no-go/escalation.
3. Keep `Blocked` for missing input, authority, environment, unsafe target, or no valid branch.
4. Add branch tag inference and definition lint warning for ambiguous branch names.
5. Add tests for QA repair branch, approval no-go branch, and non-software review with negative disposition.

## Scope Exceptions

None. Keep scope generic and PostgreSQL-only.

## Do Not Do

- Do not hardcode Blazor/.NET behavior into generic process runtime.
- Do not mix workflow internal status with process step completion.
- Do not add SQLite migrations or provider-switching logic.
- Do not close from prompt-only changes.
- Do not satisfy required artifacts with diagnostic placeholders.

## Acceptance Checklist

- [x] Add `ProcessDispositionRouter` or equivalent function after finalizer validation.
- [x] Map artifact/proof failures to branch outcomes when branch definitions represent repair/rework/no-go/escalation.
- [x] Keep `Blocked` for missing input, authority, environment, unsafe target, or no valid branch.
- [x] Add branch tag inference and definition lint warning for ambiguous branch names.
- [x] Add tests for QA repair branch, approval no-go branch, and non-software review with negative disposition.

## Proof Required

- `bundle://proof/SB03/manifest.md`
- `bundle://proof/SB03/semantic-invariants.md`
- `bundle://proof/SB03/transcripts/failing-first.txt`
- `bundle://proof/SB03/transcripts/passing.txt`
- `bundle://proof/SB03/transcripts/source-assertions.txt`
- `bundle://proof/SB03/transcripts/anti-stub-audit.txt`
- `bundle://proof/SB03/transcripts/changed-file-hashes.txt`

## Browser Validation Logging

- N/A unless this subbundle changes browser-visible process flows or SB08 runs browser proof scenarios. If browser proof is used, record route, viewport, actions, screenshots, console, and result in `reviews/01-execution-report.md`.

## Progression Gate

- Do not start downstream dependent subbundles until this subbundle's proof manifest is complete and the targeted tests pass.

## Suggested Agent Prompt

Implement `Disposition Routing Instead Of Hard Blocking` from `codex/bundles/processes-hardening-followup-scope-resilience`. Preserve generic process semantics, keep workflows subordinate to processes, and update proof files before marking the subbundle complete.




