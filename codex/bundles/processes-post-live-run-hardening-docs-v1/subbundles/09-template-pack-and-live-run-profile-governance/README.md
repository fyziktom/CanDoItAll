# SB09: Template Pack And Live Run Profile Governance

## Status

- Status: Completed

## Objective

- Update template pack and live-run profiles after real-run learning.

## Covered Inputs

- RN09 maps to RQ09.
- Preserve the original bundle scope for this subbundle.

## Prerequisites

- SB06 completed.

## Exact Source References

- repo://Templates/Processes/README.md
- repo://Templates/Processes/manifest.json
- repo://Templates/Processes/seed-catalog/live-run-profiles.json
- repo://src/CanDoItAll.Modules.Processes/Templates/ProcessTemplatePackLoader.cs

## Deliverables

- Template governance updates and live-run profile guidance that avoids seeded transitions/artifacts.

## Dependency Impact

- SB14 and SB17 rely on current template/profile guidance.

## Validation Depth

- Template and docs proof plus tests or validators where available.
- Include failing-first or adversarial proof when behavior changes, passing proof, source assertions, anti-stub audit, changed-file hashes, classification, and proof-debt closure status.

## Implementation Steps

- Inspect the referenced source and nearby tests.
- Implement the smallest correct change set for this subbundle only.
- Update proof artifacts under bundle://proof/SB09/.

## Do Not Do

- Do not hard-code Blazor, Tetris, project ids, run ids, local paths, or user-specific paths.
- Do not use docs-only changes to satisfy runtime proof requirements.

## Acceptance Checklist

- Deliverables are complete.
- Required tests or source assertions are recorded.
- Execution report gate rows are updated.

## Closure Evidence

- Template README: repo://Templates/Processes/README.md
- Template manifest: repo://Templates/Processes/manifest.json
- Live-run profiles: repo://Templates/Processes/seed-catalog/live-run-profiles.json
- Baseline scenarios: repo://Templates/Processes/seed-catalog/baseline-scenarios.json
- Typed model: repo://src/CanDoItAll.Modules.Processes/Templates/ProcessTemplatePackScenarios.cs
- Loader proof source: repo://src/CanDoItAll.Modules.Processes/Templates/ProcessTemplatePackLoader.cs
- Tests: repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateGovernanceTests.cs
- Manifest: bundle://proof/SB09/manifest.md
- Semantic invariants: bundle://proof/SB09/semantic-invariants.md
- Passing proof: bundle://proof/SB09/transcripts/passing.txt
- Adversarial proof: bundle://proof/SB09/transcripts/failing-first.txt
- Browser validation: N/A; template/profile governance only.

## Proof Required

- bundle://proof/SB09/manifest.md
- bundle://proof/SB09/semantic-invariants.md when the subbundle is critical or behavior-changing.
- Command transcripts under bundle://proof/SB09/transcripts/.

## Browser Validation Logging

- N/A.
- Update bundle://reviews/01-execution-report.md if browser proof is applicable.

## Progression Gate

- SB14 and SB17 may start once template/profile governance is current.

## Suggested Agent Prompt

- Execute SB09 literally, preserve runtime genericity, and close owned proof before moving downstream.
