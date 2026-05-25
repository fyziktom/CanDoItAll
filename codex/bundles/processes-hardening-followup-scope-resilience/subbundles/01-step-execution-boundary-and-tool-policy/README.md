# Step Execution Boundary And Tool Policy

## Status

- Completed

## Objective

Prevent architecture/planning/review steps from doing downstream implementation by adding generic operation policy and tool-level enforcement.

## Covered Inputs

- Original notes: see `bundle://inputs/02-structured-input.md`
- Requirements: RQ01, RQ02, RQ11, RQ12

## Prerequisites

- None.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Execution.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExecutionPrompt.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjection.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`

## Deliverables

- Introduce `ProcessStepExecutionBoundary` or equivalent runtime object.
- Compute boundary from explicit step definition data where available and conservative inference fallback.
- Pass boundary through `BuildProcessInvocationMetadataJson` / `ExecutionInvocationMetadata`.
- Update workspace/tool policy bridge so denied operations are rejected before mutation.

## Dependency Impact

- Critical foundation. Downstream subbundles depend on trustworthy operation boundaries.

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

1. Introduce `ProcessStepExecutionBoundary` or equivalent runtime object.
2. Compute boundary from explicit step definition data where available and conservative inference fallback.
3. Pass boundary through `BuildProcessInvocationMetadataJson` / `ExecutionInvocationMetadata`.
4. Update workspace/tool policy bridge so denied operations are rejected before mutation.
5. Architecture/design/scope steps may write managed artifacts but may not mutate product/external-target roots unless explicitly allowed.
6. Add red-team test: Blazor architecture step attempts product mutation; tool policy rejects it and architecture artifact can still be written.

## Scope Exceptions

None. Keep scope generic and PostgreSQL-only.

## Do Not Do

- Do not hardcode Blazor/.NET behavior into generic process runtime.
- Do not mix workflow internal status with process step completion.
- Do not add SQLite migrations or provider-switching logic.
- Do not close from prompt-only changes.
- Do not satisfy required artifacts with diagnostic placeholders.

## Acceptance Checklist

- [x] Introduce `ProcessStepExecutionBoundary` or equivalent runtime object.
- [x] Compute boundary from explicit step definition data where available and conservative inference fallback.
- [x] Pass boundary through `BuildProcessInvocationMetadataJson` / `ExecutionInvocationMetadata`.
- [x] Update workspace/tool policy bridge so denied operations are rejected before mutation.
- [x] Architecture/design/scope steps may write managed artifacts but may not mutate product/external-target roots unless explicitly allowed.
- [x] Add red-team test: Blazor architecture step attempts product mutation; tool policy rejects it and architecture artifact can still be written.

## Proof Required

- `bundle://proof/SB01/manifest.md`
- `bundle://proof/SB01/semantic-invariants.md`
- `bundle://proof/SB01/transcripts/failing-first.txt`
- `bundle://proof/SB01/transcripts/passing.txt`
- `bundle://proof/SB01/transcripts/source-assertions.txt`
- `bundle://proof/SB01/transcripts/anti-stub-audit.txt`
- `bundle://proof/SB01/transcripts/changed-file-hashes.txt`

## Browser Validation Logging

- N/A unless this subbundle changes browser-visible process flows or SB08 runs browser proof scenarios. If browser proof is used, record route, viewport, actions, screenshots, console, and result in `reviews/01-execution-report.md`.

## Progression Gate

- Do not start downstream dependent subbundles until this subbundle's proof manifest is complete and the targeted tests pass.

## Suggested Agent Prompt

Implement `Step Execution Boundary And Tool Policy` from `codex/bundles/processes-hardening-followup-scope-resilience`. Preserve generic process semantics, keep workflows subordinate to processes, and update proof files before marking the subbundle complete.




