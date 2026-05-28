# SB10: Agent Skill Tool Matrix And Anti Improvisation

## Status

- Status: Completed

## Objective

- Ensure agents have needed skills/tools and do not improvise process operations.

## Covered Inputs

- RN10 maps to RQ10.
- Preserve the original bundle scope for this subbundle.

## Prerequisites

- SB07 completed.

## Exact Source References

- repo://src/CanDoItAll.AgentFramework.Core/README.md
- repo://tests/CanDoItAll.Tests.Unit/AgentToolInvocationPolicyTests.cs
- repo://tests/CanDoItAll.Tests.Integration/AgentFrameworkExecutionCapabilityFilteringIntegrationTests.cs
- repo://codex/skills/candoitall-api-processes/SKILL.md

## Deliverables

- Role-by-role skill/tool matrix and typed missing capability diagnostics.

## Dependency Impact

- SB11, SB12, and SB18 rely on anti-improvisation policy.

## Validation Depth

- Critical foundation if runtime gating changes; requires negative missing-tool/skill proof.
- Include failing-first or adversarial proof when behavior changes, passing proof, source assertions, anti-stub audit, changed-file hashes, classification, and proof-debt closure status.

## Implementation Steps

- Inspect the referenced source and nearby tests.
- Implement the smallest correct change set for this subbundle only.
- Update proof artifacts under bundle://proof/SB10/.

## Do Not Do

- Do not hard-code Blazor, Tetris, project ids, run ids, local paths, or user-specific paths.
- Do not use docs-only changes to satisfy runtime proof requirements.

## Acceptance Checklist

- Deliverables are complete.
- Required tests and source assertions are recorded under `bundle://proof/SB10/transcripts/`.
- Execution report gate rows are updated.

## Closure Notes

- Added typed `AgentCapabilityRequirement`, `AgentCapabilityDiagnostic`, and `AgentCapabilityRequirementEvaluation` contracts.
- Added `AgentCapabilityRequirementEvaluator` and reused its retired-capability predicate in runtime capability composition.
- Added process role skill/tool matrices to AgentFramework Core README and `candoitall-api-processes` skill, then synced the active Codex skill copy.
- Passing proof: `bundle://proof/SB10/transcripts/passing.txt`.
- Adversarial proof: `bundle://proof/SB10/transcripts/failing-first.txt`.

## Proof Required

- bundle://proof/SB10/manifest.md
- bundle://proof/SB10/semantic-invariants.md when the subbundle is critical or behavior-changing.
- Command transcripts under bundle://proof/SB10/transcripts/.

## Browser Validation Logging

- N/A unless UI capability proof changes.
- Update bundle://reviews/01-execution-report.md if browser proof is applicable.

## Progression Gate

- SB11 and SB12 may rely on tool/skill behavior after typed negative proof passes.

## Suggested Agent Prompt

- Execute SB10 literally, preserve runtime genericity, and close owned proof before moving downstream.
