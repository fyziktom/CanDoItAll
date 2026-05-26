# SB10: Artifact Contracts Lineage And Current Run Proof

## Status

- Status: `Completed`

## Objective

Ensure generic Blazor WASM PWA required artifacts include implementation change set, self-review, runtime evidence, validation review, run evidence index, and project-structure writeback summary with current-run lineage.

## Covered Inputs

- RQ03 artifact expectations.
- RQ07 blockers and health.
- RQ09 fake-proof resistance.

## Prerequisites

- SB09 work brief hardening is complete.

## Exact Source References

- `repo://Templates/Processes/processes/blazor-app-delivery/definition.json`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactValidation.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjection.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`

## Deliverables

- Artifact contract and lineage checks that reject stale, seeded, missing, or chat-only proof.
- Tests for missing screenshot/console proof, stale artifact rejection, and required artifact materialization blockers.

## Dependency Impact

- SB11 writeback and SB12 health depend on accurate artifact satisfaction state.

## Validation Depth

- Critical foundation. Require Semantic Adequacy Gate proof including stale/seeded negative cases and current-run positive cases.

## Implementation Steps

1. Audit Blazor WASM PWA artifact expectations and runtime satisfaction rules.
2. Add missing required artifacts and validation summaries.
3. Add tests for stale, missing, and current-run proof.
4. Record source assertions and anti-stub audit.

## Do Not Do

- Do not let seeded regression artifacts satisfy live-run proof.
- Do not count chat-only summaries, blank screenshots, or stale console logs as proof.

## Acceptance Checklist

- Current-run implementation proof is required.
- Runtime/browser evidence includes screenshot and console proof when UI-visible.
- Artifact lineage is preserved.
- Missing proof blocks with typed cause and recovery options.

## Proof Required

- `proof/SB10/manifest.md`
- `proof/SB10/semantic-invariants.md`
- `proof/SB10/transcripts/failing-first.txt`
- `proof/SB10/transcripts/passing.txt`

## Browser Validation Logging

- N/A unless browser-visible proof UI changes; evidence contracts are validated by tests.

## Progression Gate

- SB11 may start only after current-run artifact satisfaction is proven against stale and missing proof negatives.

## Suggested Agent Prompt

Harden artifact contracts and lineage so generic Blazor WASM PWA live runs require current, materialized proof and reject seeded or stale evidence.
