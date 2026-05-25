# Process Definition Lint And Template Quality

## Status

Ready.

## Objective

Catch ambiguous or unsafe process definitions before agents execute them.

## Covered Inputs

- Original notes: see `bundle://inputs/02-structured-input.md`
- Requirements: RQ10, RQ11, RQ12

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

- Add `ProcessDefinitionLinter` or extend existing validation.
- Lint step execution boundaries, artifact mappings, role/workflow/subprocess assignments, branch outcomes, required inputs, and tool policies.
- Add dry-run simulation output for process authors.
- Add lint tests for Blazor architecture-step-over-implementation, workflow artifact mapping gap, subprocess parent mapping gap, finance approval missing branch, and legal decision-log artifact.

## Dependency Impact

Definition quality layer to reduce runtime failures.

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

1. Add `ProcessDefinitionLinter` or extend existing validation.
2. Lint step execution boundaries, artifact mappings, role/workflow/subprocess assignments, branch outcomes, required inputs, and tool policies.
3. Add dry-run simulation output for process authors.
4. Add lint tests for Blazor architecture-step-over-implementation, workflow artifact mapping gap, subprocess parent mapping gap, finance approval missing branch, and legal decision-log artifact.

## Scope Exceptions

None. Keep scope generic and PostgreSQL-only.

## Do Not Do

- Do not hardcode Blazor/.NET behavior into generic process runtime.
- Do not mix workflow internal status with process step completion.
- Do not add SQLite migrations or provider-switching logic.
- Do not close from prompt-only changes.
- Do not satisfy required artifacts with diagnostic placeholders.

## Acceptance Checklist

- [ ] Add `ProcessDefinitionLinter` or extend existing validation.
- [ ] Lint step execution boundaries, artifact mappings, role/workflow/subprocess assignments, branch outcomes, required inputs, and tool policies.
- [ ] Add dry-run simulation output for process authors.
- [ ] Add lint tests for Blazor architecture-step-over-implementation, workflow artifact mapping gap, subprocess parent mapping gap, finance approval missing branch, and legal decision-log artifact.

## Proof Required

- `bundle://proof/SB07/manifest.md`
- `bundle://proof/SB07/semantic-invariants.md`
- `bundle://proof/SB07/transcripts/failing-first.txt`
- `bundle://proof/SB07/transcripts/passing.txt`
- `bundle://proof/SB07/transcripts/source-assertions.txt`
- `bundle://proof/SB07/transcripts/anti-stub-audit.txt`
- `bundle://proof/SB07/transcripts/changed-file-hashes.txt`

## Browser Validation Logging

N/A unless this subbundle changes browser-visible process flows or SB08 runs browser proof scenarios. If browser proof is used, record route, viewport, actions, screenshots, console, and result in `reviews/01-execution-report.md`.

## Progression Gate

Do not start downstream dependent subbundles until this subbundle's proof manifest is complete and the targeted tests pass.

## Suggested Agent Prompt

Implement `Process Definition Lint And Template Quality` from `codex/bundles/processes-hardening-followup-scope-resilience`. Preserve generic process semantics, keep workflows subordinate to processes, and update proof files before marking the subbundle complete.
