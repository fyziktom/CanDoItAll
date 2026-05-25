# Red-Team Validation Suite

## Status

Ready.

## Objective

Prove the hardening with realistic process scenarios and final closure gates.

## Covered Inputs

- Original notes: see `bundle://inputs/02-structured-input.md`
- Requirements: RQ01, RQ02, RQ03, RQ04, RQ05, RQ06, RQ07, RQ08, RQ09, RQ10, RQ11, RQ12

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

- Run targeted integration tests for SB01-SB07.
- Add scenario tests: Blazor architecture cannot implement, implementation can mutate only in implementation step, QA repair branch, workflow-backed artifact contract, subprocess missing child artifact, upstream artifact unblock.
- Add non-software scenarios: finance approval, legal decision log, HR screen, operations incident review.
- Run solution build and relevant test projects.

## Dependency Impact

Final closure. Cannot pass if any critical subbundle lacks artifact-backed proof.

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

1. Run targeted integration tests for SB01-SB07.
2. Add scenario tests: Blazor architecture cannot implement, implementation can mutate only in implementation step, QA repair branch, workflow-backed artifact contract, subprocess missing child artifact, upstream artifact unblock.
3. Add non-software scenarios: finance approval, legal decision log, HR screen, operations incident review.
4. Run solution build and relevant test projects.
5. Record changed-file hashes and proof manifests.

## Scope Exceptions

None. Keep scope generic and PostgreSQL-only.

## Do Not Do

- Do not hardcode Blazor/.NET behavior into generic process runtime.
- Do not mix workflow internal status with process step completion.
- Do not add SQLite migrations or provider-switching logic.
- Do not close from prompt-only changes.
- Do not satisfy required artifacts with diagnostic placeholders.

## Acceptance Checklist

- [ ] Run targeted integration tests for SB01-SB07.
- [ ] Add scenario tests: Blazor architecture cannot implement, implementation can mutate only in implementation step, QA repair branch, workflow-backed artifact contract, subprocess missing child artifact, upstream artifact unblock.
- [ ] Add non-software scenarios: finance approval, legal decision log, HR screen, operations incident review.
- [ ] Run solution build and relevant test projects.
- [ ] Record changed-file hashes and proof manifests.

## Proof Required

- `bundle://proof/SB08/manifest.md`
- `bundle://proof/SB08/semantic-invariants.md`
- `bundle://proof/SB08/transcripts/failing-first.txt`
- `bundle://proof/SB08/transcripts/passing.txt`
- `bundle://proof/SB08/transcripts/source-assertions.txt`
- `bundle://proof/SB08/transcripts/anti-stub-audit.txt`
- `bundle://proof/SB08/transcripts/changed-file-hashes.txt`

## Browser Validation Logging

N/A unless this subbundle changes browser-visible process flows or SB08 runs browser proof scenarios. If browser proof is used, record route, viewport, actions, screenshots, console, and result in `reviews/01-execution-report.md`.

## Progression Gate

Do not start downstream dependent subbundles until this subbundle's proof manifest is complete and the targeted tests pass.

## Suggested Agent Prompt

Implement `Red-Team Validation Suite` from `codex/bundles/processes-hardening-followup-scope-resilience`. Preserve generic process semantics, keep workflows subordinate to processes, and update proof files before marking the subbundle complete.
