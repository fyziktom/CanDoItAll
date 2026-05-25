# Artifact Validation Tuning And Lineage

## Status

Ready.

## Objective

Make artifact validation generic, less heuristic, and stronger on current-run lineage.

## Covered Inputs

- Original notes: see `bundle://inputs/02-structured-input.md`
- Requirements: RQ07, RQ08, RQ11, RQ12

## Prerequisites

Complete prerequisite subbundles according to `bundle://plan/01-phase-plan.md`.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Execution.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExecutionPrompt.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjection.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`

## Deliverables

- Add explicit contract/mode fields or a normalized contract object; keep text summary as fallback.
- Refine mode detection: do not treat every `log` as runtime proof.
- Refine placeholder detection: do not reject legitimate TODO registers, legal unavailable findings, or missing-artifact analysis deliverables.
- Validate JSON content when JSON is required, not only extension.

## Dependency Impact

Critical. Prevents false blocks and stale artifact acceptance.

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

1. Add explicit contract/mode fields or a normalized contract object; keep text summary as fallback.
2. Refine mode detection: do not treat every `log` as runtime proof.
3. Refine placeholder detection: do not reject legitimate TODO registers, legal unavailable findings, or missing-artifact analysis deliverables.
4. Validate JSON content when JSON is required, not only extension.
5. Enforce execution/workflow/recovery lineage for all producer kinds, not only existing-managed files.
6. Add non-software generic tests.

## Scope Exceptions

None. Keep scope generic and PostgreSQL-only.

## Do Not Do

- Do not hardcode Blazor/.NET behavior into generic process runtime.
- Do not mix workflow internal status with process step completion.
- Do not add SQLite migrations or provider-switching logic.
- Do not close from prompt-only changes.
- Do not satisfy required artifacts with diagnostic placeholders.

## Acceptance Checklist

- [ ] Add explicit contract/mode fields or a normalized contract object; keep text summary as fallback.
- [ ] Refine mode detection: do not treat every `log` as runtime proof.
- [ ] Refine placeholder detection: do not reject legitimate TODO registers, legal unavailable findings, or missing-artifact analysis deliverables.
- [ ] Validate JSON content when JSON is required, not only extension.
- [ ] Enforce execution/workflow/recovery lineage for all producer kinds, not only existing-managed files.
- [ ] Add non-software generic tests.

## Proof Required

- `bundle://proof/SB05/manifest.md`
- `bundle://proof/SB05/semantic-invariants.md`
- `bundle://proof/SB05/transcripts/failing-first.txt`
- `bundle://proof/SB05/transcripts/passing.txt`
- `bundle://proof/SB05/transcripts/source-assertions.txt`
- `bundle://proof/SB05/transcripts/anti-stub-audit.txt`
- `bundle://proof/SB05/transcripts/changed-file-hashes.txt`

## Browser Validation Logging

N/A unless this subbundle changes browser-visible process flows or SB08 runs browser proof scenarios. If browser proof is used, record route, viewport, actions, screenshots, console, and result in `reviews/01-execution-report.md`.

## Progression Gate

Do not start downstream dependent subbundles until this subbundle's proof manifest is complete and the targeted tests pass.

## Suggested Agent Prompt

Implement `Artifact Validation Tuning And Lineage` from `codex/bundles/processes-hardening-followup-scope-resilience`. Preserve generic process semantics, keep workflows subordinate to processes, and update proof files before marking the subbundle complete.
