# SB04: Artifact Storage Lineage Dedupe And Retention Hardening

## Status

- Status: Completed

## Objective

- Harden artifact identity, storage, hash, dedupe, and retention behavior.

## Covered Inputs

- RN04 maps to RQ04.
- Preserve the original bundle scope for this subbundle.

## Prerequisites

- SB03 completed.

## Exact Source References

- repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessArtifactIdentityService.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjection.cs
- repo://src/CanDoItAll.Modules.Processes/Persistence/Entities/ProcessRuntimeModels.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs

## Deliverables

- Race/concurrency proof and retention guidance for artifact storage surfaces.

## Dependency Impact

- SB08 and SB13 rely on trustworthy artifact lineage and recovery proof.

## Validation Depth

- Critical foundation with adversarial stale-record and hash/dedupe proof.
- Include failing-first or adversarial proof when behavior changes, passing proof, source assertions, anti-stub audit, changed-file hashes, classification, and proof-debt closure status.

## Implementation Steps

- Inspect the referenced source and nearby tests.
- Implement the smallest correct change set for this subbundle only.
- Update proof artifacts under bundle://proof/SB04/.

## Do Not Do

- Do not hard-code Blazor, Tetris, project ids, run ids, local paths, or user-specific paths.
- Do not use docs-only changes to satisfy runtime proof requirements.

## Acceptance Checklist

- Deliverables are complete.
- Required tests or source assertions are recorded.
- Execution report gate rows are updated.

## Proof Required

- bundle://proof/SB04/manifest.md
- bundle://proof/SB04/semantic-invariants.md when the subbundle is critical or behavior-changing.
- Command transcripts under bundle://proof/SB04/transcripts/.

## Browser Validation Logging

- N/A unless operator UI for stale records changes.
- Update bundle://reviews/01-execution-report.md if browser proof is applicable.

## Progression Gate

- SB08 and SB13 may rely on artifact storage only after stale/race proof passes.

## Closure Evidence

- Manifest: bundle://proof/SB04/manifest.md
- Semantic invariants: bundle://proof/SB04/semantic-invariants.md
- Passing runtime/source tests: bundle://proof/SB04/transcripts/passing.txt
- Failing-first unguarded-save check: bundle://proof/SB04/transcripts/failing-first.txt
- Source assertions: bundle://proof/SB04/transcripts/source-assertions.txt

## Suggested Agent Prompt

- Execute SB04 literally, preserve runtime genericity, and close owned proof before moving downstream.
