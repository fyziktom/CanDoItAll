# SB03: Artifact Validation Read Model And Health Consolidation

## Status

- Status: Completed

## Objective

- Ensure artifact validation, read model, health, recovery, API, and UI share status semantics.

## Covered Inputs

- RN03 maps to RQ03.
- Preserve the original bundle scope for this subbundle.

## Prerequisites

- SB02 boundary map completed.

## Exact Source References

- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactValidation.cs
- repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.RuntimeReadQuery.cs
- repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessHealthInvariantAuditor.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessRuntimeReadQueryServiceTests.cs

## Deliverables

- Shared artifact validation/status projection behavior or verified existing equivalent with matrix tests.

## Dependency Impact

- SB04, SB11, SB13, and SB16 rely on these semantics.

## Validation Depth

- Critical foundation with semantic positive and adversarial negative proof for invalid/stale artifacts.
- Include failing-first or adversarial proof when behavior changes, passing proof, source assertions, anti-stub audit, changed-file hashes, classification, and proof-debt closure status.

## Implementation Steps

- Inspect the referenced source and nearby tests.
- Implement the smallest correct change set for this subbundle only.
- Update proof artifacts under bundle://proof/SB03/.

## Do Not Do

- Do not hard-code Blazor, Tetris, project ids, run ids, local paths, or user-specific paths.
- Do not use docs-only changes to satisfy runtime proof requirements.

## Acceptance Checklist

- Deliverables are complete.
- Required tests or source assertions are recorded.
- Execution report gate rows are updated.

## Proof Required

- bundle://proof/SB03/manifest.md
- bundle://proof/SB03/semantic-invariants.md when the subbundle is critical or behavior-changing.
- Command transcripts under bundle://proof/SB03/transcripts/.

## Browser Validation Logging

- Only required if UI projection changes; otherwise N/A with component/API proof.
- Update bundle://reviews/01-execution-report.md if browser proof is applicable.

## Progression Gate

- SB04, SB11, and SB13 may start only after status semantics have transcript-backed proof.

## Closure Evidence

- Manifest: bundle://proof/SB03/manifest.md
- Semantic invariants: bundle://proof/SB03/semantic-invariants.md
- Passing projection matrix: bundle://proof/SB03/transcripts/sb03-projection-service-tests.txt
- Passing read-model regression: bundle://proof/SB03/transcripts/sb03-read-model-regression-tests.txt
- Adversarial duplicate-removal proof: bundle://proof/SB03/transcripts/sb03-adversarial-duplicate-mapping-removed.txt

## Suggested Agent Prompt

- Execute SB03 literally, preserve runtime genericity, and close owned proof before moving downstream.
