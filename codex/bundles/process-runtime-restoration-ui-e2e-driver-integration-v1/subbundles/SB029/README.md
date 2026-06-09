# SB029 — Run business-analysis scenario and assert analysis artifact/evidence/status.

## Status

- Status: `Completed`

## Objective

Implement the `Run business-analysis scenario and assert analysis artifact/evidence/status.` slice as part of `P10: Generic business-analysis scenario`.

## Covered Inputs

- Raw user request in `bundle://inputs/raw-request.md`
- Requirements in `bundle://requirements/01-normalized-requirements.md`
- Phase plan in `bundle://plan/01-phase-plan.md`

## Prerequisites

- Previous subbundles in the phase must be completed. If this is a critical gate, all prior subbundles in the phase must have source-backed proof.

## Exact Source References

- `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
- `repo://src/CanDoItAll.Modules.Processes`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch`
- `repo://src/CanDoItAll.Processes.Core`
- `repo://src/CanDoItAll.Processes.Drivers.Abstractions`
- `repo://src/CanDoItAll.Processes.Drivers.ArtifactEvidence`
- `repo://src/CanDoItAll.Processes.Drivers.BusinessAnalysis`
- `repo://src/CanDoItAll.Processes.Drivers.ObservationAggregation`
- `repo://src/CanDoItAll.Processes.Drivers.OfficeEvidence`
- `repo://src/CanDoItAll.Processes.Drivers.RuntimeEvidence`
- `repo://src/CanDoItAll.Processes.Drivers.TranscriptVerification`
- `repo://src/CanDoItAll.Processes.Drivers.VerificationGateway`
- `repo://tests/CanDoItAll.Tests.Integration`
- `repo://tests/CanDoItAll.Tests.Playwright`
- `repo://src/CanDoItAll.Web`
- `repo://src/CanDoItAll.Composition`

## Deliverables

- Source changes or proof artifacts required by this slice.
- Tests or scans proving behavior.
- Updated execution report row for `SB029`.

## Dependency Impact

- Downstream phases depend on this subbundle preserving process runtime behavior and Core/driver boundaries. If this subbundle changes API shape, update the relevant source-backed guards.

## Validation Depth

- Focused validation sufficient, but must leave evidence for nearest critical gate.

## Implementation Steps

1. Re-read the exact current source files before editing.
2. Make the smallest complete change for this slice.
3. Add or update tests before relying on manual proof.
4. Run focused validation.
5. Record command transcript paths and source assertions.

## Scope Exceptions

Do not implement generic runtime driver host, registry, selector, DI registration, manager command, scheduler/workflow driver hook, shell execution, Office/Graph calls, workspace/storage writes, process mutation, claim mutation, transition mutation, finalizer application, or retry scheduling.

## Do Not Do

- Do not add tests that depend on `codex/bundles/<bundle-name>`.
- Do not weaken architecture guards by deleting them without replacement.
- Do not use small/medium/mobile browser proof.
- Do not make driver verification mutate process state.

## Acceptance Checklist

- [x] Source changes are source-backed and minimal.
- [x] Tests/scans prove the intended behavior.
- [x] No transient bundle path dependency added.
- [x] No forbidden runtime/driver/Core/UI drift.
- [x] Execution report row updated.

## Proof Required

- Record focused test/scans and feed nearest critical proof manifest.

## Browser Validation Logging

- N/A unless this subbundle unexpectedly changes browser-visible UI. If UI files change unexpectedly, fail and re-scope.

## Progression Gate

- Do not proceed to dependent subbundles if this proof fails or if any hard-stop condition in `analysis/02-assumptions-and-risks.md` is triggered.

## Suggested Agent Prompt

Implement `SB029 — Run business-analysis scenario and assert analysis artifact/evidence/status.`. Preserve all hard constraints. Produce source-backed proof and update the execution report.



