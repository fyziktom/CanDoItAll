# Workflow And Subprocess Finalizer Coverage

## Status

- Completed

## Objective

Make workflow-backed roles and subprocess parent steps obey the same process artifact/finalizer contract as direct agents.

## Covered Inputs

- Original notes: see `bundle://inputs/02-structured-input.md`
- Requirements: RQ03, RQ04, RQ08, RQ12

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

- Workflow-backed `DispatchCandidate` must load expected artifacts, artifact inputs, branch outcomes, recorded ids, and cooperation metadata.
- Add workflow output-to-process artifact projection/linking if missing.
- Subprocess parent completion must call `FinalizeStepCompletionAsync` before transition.
- Subprocess source-less projections must become diagnostics/gap records, not satisfying artifact records.

## Dependency Impact

- Critical foundation. Artifact reliability is false if workflow/subprocess paths are weaker.

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

1. Workflow-backed `DispatchCandidate` must load expected artifacts, artifact inputs, branch outcomes, recorded ids, and cooperation metadata.
2. Add workflow output-to-process artifact projection/linking if missing.
3. Subprocess parent completion must call `FinalizeStepCompletionAsync` before transition.
4. Subprocess source-less projections must become diagnostics/gap records, not satisfying artifact records.
5. Add tests proving workflow and subprocess required artifacts cannot be bypassed.

## Scope Exceptions

None. Keep scope generic and PostgreSQL-only.

## Do Not Do

- Do not hardcode Blazor/.NET behavior into generic process runtime.
- Do not mix workflow internal status with process step completion.
- Do not add SQLite migrations or provider-switching logic.
- Do not close from prompt-only changes.
- Do not satisfy required artifacts with diagnostic placeholders.

## Acceptance Checklist

- [x] Workflow-backed `DispatchCandidate` must load expected artifacts, artifact inputs, branch outcomes, recorded ids, and cooperation metadata.
- [x] Add workflow output-to-process artifact projection/linking if missing.
- [x] Subprocess parent completion must call `FinalizeStepCompletionAsync` before transition.
- [x] Subprocess source-less projections must become diagnostics/gap records, not satisfying artifact records.
- [x] Add tests proving workflow and subprocess required artifacts cannot be bypassed.

## Proof Required

- `bundle://proof/SB02/manifest.md`
- `bundle://proof/SB02/semantic-invariants.md`
- `bundle://proof/SB02/transcripts/failing-first.txt`
- `bundle://proof/SB02/transcripts/passing.txt`
- `bundle://proof/SB02/transcripts/source-assertions.txt`
- `bundle://proof/SB02/transcripts/anti-stub-audit.txt`
- `bundle://proof/SB02/transcripts/changed-file-hashes.txt`

## Browser Validation Logging

- N/A unless this subbundle changes browser-visible process flows or SB08 runs browser proof scenarios. If browser proof is used, record route, viewport, actions, screenshots, console, and result in `reviews/01-execution-report.md`.

## Progression Gate

- Do not start downstream dependent subbundles until this subbundle's proof manifest is complete and the targeted tests pass.

## Suggested Agent Prompt

Implement `Workflow And Subprocess Finalizer Coverage` from `codex/bundles/processes-hardening-followup-scope-resilience`. Preserve generic process semantics, keep workflows subordinate to processes, and update proof files before marking the subbundle complete.




