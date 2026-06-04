# SB14: Generic Nonsoftware And Agent Training Processes

## Status

- Status: Completed

## Objective

- Protect generic Processes behavior beyond software delivery.

## Covered Inputs

- RN14 maps to RQ14.
- Preserve the original bundle scope for this subbundle.

## Prerequisites

- SB09 completed.

## Exact Source References

- repo://Templates/Processes/manifest.json
- repo://Templates/Processes/seed-catalog/baseline-scenarios.json
- repo://tests/CanDoItAll.Tests.Integration/BusinessPlanProcessPostgresIntegrationTests.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateGovernanceTests.cs

## Deliverables

- Governance tests, an agent-training skeleton if missing, and generic examples.

## Dependency Impact

- SB18 red-team uses these scenarios to reject software-only assumptions.

## Validation Depth

- Template/governance tests plus source assertions.
- Include failing-first or adversarial proof when behavior changes, passing proof, source assertions, anti-stub audit, changed-file hashes, classification, and proof-debt closure status.

## Implementation Steps

- Inspect the referenced source and nearby tests.
- Implement the smallest correct change set for this subbundle only.
- Update proof artifacts under bundle://proof/SB14/.

## Do Not Do

- Do not hard-code Blazor, Tetris, project ids, run ids, local paths, or user-specific paths.
- Do not use docs-only changes to satisfy runtime proof requirements.

## Acceptance Checklist

- Deliverables are complete.
- Required tests or source assertions are recorded.
- Execution report gate rows are updated.

## Proof Required

- bundle://proof/SB14/manifest.md
- bundle://proof/SB14/semantic-invariants.md when the subbundle is critical or behavior-changing.
- Command transcripts under bundle://proof/SB14/transcripts/.

## Browser Validation Logging

- N/A unless a generic process UI changes.
- Update bundle://reviews/01-execution-report.md if browser proof is applicable.

## Progression Gate

- SB18 may start only after generic process assumptions are tested or explicitly blocked.

## Suggested Agent Prompt

- Execute SB14 literally, preserve runtime genericity, and close owned proof before moving downstream.
