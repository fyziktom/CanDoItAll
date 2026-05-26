# SB14: Run generic software and non-software red-team scenarios.

## Objective

Run generic software and non-software red-team scenarios.

## Why This Matters

This subbundle closes a concrete runtime correctness gap observed after phase5. The process runtime must avoid both false completion and unnecessary blocking while staying generic.

## Implementation Tasks

- Add scenario harness cases: architecture-only software step, business plan external artifact destination, legal approval, manufacturing QA, incident response, workflow-backed role, subprocess parent, manager recovery.
- Validate no architecture/planning step mutates product targets.
- Validate manual/API completion cannot bypass artifact validation.
- Validate recovery router selects correct next action.
- Run final full validation and completed-stage bundle validator.

## Required Tests

- Add failing-first or red-team tests before the production fix where practical.
- Add positive tests proving the fixed behavior.
- Include at least one generic/non-software case if this subbundle changes generic process semantics.

## Closure Criteria

- Production code implements the behavior; no prompt-only fix.
- Proof manifest is updated.
- Focused tests pass.
- No SQLite runtime/migration dependency is introduced.

## Status

- Completed

## Covered Inputs

- RN10 add generic red-team coverage across software and non-software processes.
- RQ12 red-team coverage.
- All raw notes RN01 through RN10 for final closure.

## Prerequisites

- SB01 through SB13 closure gates pass or are honestly blocked with follow-up subbundles.
- Required proof manifests exist for all critical subbundles.

## Exact Source References

- repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessDefinitionLinterTests.cs
- repo://tests/CanDoItAll.Tests.Components/ProcessWorkspaceTests.cs
- repo://codex/bundles/processes-hardening-followup-runtime-correctness-v6/scripts/validation-commands.md

## Deliverables

- Generic scenario harness covering software and non-software processes.
- Red-team proof for architecture-only software step, business plan external artifact destination, legal approval, manufacturing QA, incident response, workflow-backed role, subprocess parent, and manager recovery.
- Final focused tests, build, PostgreSQL-only audit, completed-stage bundle validator, and raw-note closure.

## Dependency Impact

- This is the final closure subbundle; no downstream feature work may proceed from weak proof.

## Validation Depth

- Scenario tests across the required generic and software/non-software cases.
- Full focused unit/integration/component validation and solution build.
- PostgreSQL-only audit.
- Completed-stage bundle validator.
- Fake-proof resistance/red-team closure artifact.

## Implementation Steps

- Add scenario harness cases covering all listed process types.
- Validate non-mutating planning/architecture steps cannot mutate product targets.
- Validate manual/API completion cannot bypass shared artifact validation.
- Validate recovery router next actions.
- Run final validation commands and update all proof/report artifacts.

## Do Not Do

- Do not close with only happy-path software-delivery fixtures.
- Do not treat missing browser/host proof as residual risk if UI or host behavior changed.
- Do not mark raw notes solved without proof citations.

## Acceptance Checklist

- Required scenario harness cases pass.
- Product mutation, manual completion, mapping ambiguity, storage validation, and recovery routing red-team cases pass.
- Focused tests, build, PostgreSQL-only audit, and completed-stage validator pass.
- Raw-note closure table is complete.

## Proof Required

- `bundle://proof/SB14/manifest.md`
- `bundle://proof/SB14/semantic-invariants.md`
- Red-team transcript.
- Passing final validation transcripts.
- Changed-file SHA-256 transcript.
- Anti-stub audit transcript.
- Completed-stage validator transcript.

## Browser Validation Logging

- N/A unless SB13 changed rendered UI; otherwise final closure cites SB13 browser/component evidence.

## Progression Gate

- Bundle may close only after SB14 passes final validation and every raw note is marked `Solved`, `Partially solved`, or `Not solved` with proof or a concrete follow-up.

## Suggested Agent Prompt

- Execute SB14 final red-team closure, update all proof manifests and execution report rows, run final validation, and pass the completed-stage bundle validator.
