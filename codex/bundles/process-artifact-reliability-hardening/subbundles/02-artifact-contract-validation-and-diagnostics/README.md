# SB02 - Artifact Contract Validation And Diagnostics

## Status

Ready

## Objective

Replace the weak “recorded expectation id exists” completion signal with artifact contract validation that checks mode, format/schema, evidence lineage, freshness/current-run identity, and allowed producer type.

## Covered Inputs

- N001, N006, N007
- Findings F002, F005, F006, F007, F008

## Prerequisites

- SB01 finalizer exists and is used by all executor kinds.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjection.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.CompletionArtifactRecovery.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Models.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`

## Deliverables

- Artifact validation result model.
- Artifact mode/profile classification for narrative, decision, evidence, deliverable, runtime proof, and recovery diagnostic artifacts.
- Durable diagnostics for required artifact projection/validation failures.
- Finalizer uses validation results to decide completion/recovery/blocking.
- Negative guards for response text and auto-decision artifacts.

## Dependency Impact

Critical foundation for SB03 recovery and SB05 retry hardening. Recovery must consume diagnostics instead of guessing from missing ids.

## Validation Depth

Deep semantic proof required. Tests must prove invalid artifacts do not satisfy expectations even when a `ProcessArtifactRecord` exists.

## Implementation Steps

1. Add artifact validation result types.
2. Add artifact mode/profile resolution.
3. Validate required artifacts after projection and ledger reload.
4. Persist diagnostics for missing/unreadable/wrong-format/wrong-producer/stale artifacts.
5. Ensure response text projection only satisfies compatible narrative/decision expectations.
6. Ensure evidence/deliverable/runtime proof expectations require file/tool/provenance evidence.
7. Add schema/format validators where validation summaries declare JSON/Markdown/section requirements.
8. Update finalizer to use validation results instead of `RecordedArtifactExpectationIds` alone.

## Scope Exceptions

- Full rich schema editor UI is out of scope.
- Existing artifact expectation data model may be minimally extended or derived; choose the smallest durable implementation.

## Do Not Do

- Do not count diagnostics as satisfying required deliverables/evidence.
- Do not accept final assistant response as runtime proof.
- Do not hide projection failures only in logs for required artifacts.

## Acceptance Checklist

- [ ] Missing required artifact produces durable diagnostic.
- [ ] Wrong format produces durable diagnostic.
- [ ] Existing record with invalid content does not complete the step.
- [ ] Response text cannot satisfy evidence/deliverable mode unless explicitly allowed.
- [ ] Auto decision artifact cannot satisfy evidence/deliverable mode.
- [ ] Artifact validator output is visible to recovery and final transition decision.

## Proof Required

- `proof/SB02/manifest.md`
- `proof/SB02/semantic-invariants.md`
- failing-first tests for invalid record falsely satisfying expectation
- passing tests after validation model
- anti-stub audit proving production finalizer consumes validator output
- changed-file hashes

## Progression Gate

Do not start SB03 until validation results and diagnostics are consumed by the finalizer.

## Browser Validation Logging

N/A unless this subbundle adds or changes browser-visible UI. If browser proof is needed for a process scenario, record route, viewport, actions, assertions, screenshots, and result in `reviews/01-execution-report.md`.

## Suggested Agent Prompt

Use the shared implementation prompt at `bundle://shared-prompts/implementation-prompt.md`, then append this subbundle README and the exact source references above. Execute only this subbundle. Record proof before moving on.
