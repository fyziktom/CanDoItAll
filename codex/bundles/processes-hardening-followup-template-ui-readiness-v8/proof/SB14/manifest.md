# SB14 Proof Manifest

## Status

Completed.

## Summary

SB14 extends baseline scenario metadata and runtime seeding so typed templates are exercised by reusable, generic scenarios rather than prose-only sample notes. The baseline catalog now covers customer onboarding, incident response, business plan development, release readiness/deployment, architecture decision governance, and the Blazor WASM PWA/Tetris scenario with artifact creation, branch selection, blocked-state recovery metadata, and typed operation-contract exercises.

Runtime seeding now carries typed `BlockCause` values into blocked transitions and resolves existing artifacts by expectation id when one exists. That prevents same-title artifacts from satisfying the wrong required expectation during baseline replay.

## Semantic invariant

See `proof/SB14/semantic-invariants.md`.

## Production Behavior Artifact Matrix

| Signal / artifact | Producer | Consumer / lifecycle | Proof |
| --- | --- | --- | --- |
| Baseline `ContractExercises` metadata | `repo://Templates/Processes/seed-catalog/baseline-scenarios.json` loaded through `ProcessTemplatePackScenarios` | Governance regression compares every scenario exercise to projected template `AllowedOperations` and `OperationTargetScope`. | `bundle://proof/SB14/transcripts/source-assertions.txt`; `bundle://proof/SB14/transcripts/passing.txt` |
| Baseline `RecoveryExercises` metadata | Baseline scenario catalog and `ProcessTemplateBaselineRecoveryExercise` | `ProcessBlockStateClassifier` validates typed block cause and expected recovery options. | `bundle://proof/SB14/transcripts/source-assertions.txt`; `bundle://proof/SB14/transcripts/passing.txt` |
| Baseline transition `BlockCause` | Scenario transition data and `ProcessDevelopmentSeedService` | Runtime seed transitions pass the typed cause to `TransitionStepAsync` only for blocked target status. | `bundle://proof/SB14/transcripts/source-assertions.txt` |
| Baseline branch and blocked counts | `ProcessTemplatePackScenarios` summaries | HTTP Processes API and MAF process tools expose branch, blocked, contract, and recovery exercise counts. | `bundle://proof/SB14/transcripts/source-assertions.txt` |
| Expectation-id artifact reuse | `ProcessDevelopmentSeedService.RuntimeSeeds.Complex` | Seed replay matches existing artifacts by `ArtifactExpectationId` when available before falling back to title/kind matching. | `bundle://proof/SB14/transcripts/passing.txt` |

## Failing-first or adversarial proof

`proof/SB14/transcripts/failing-first.txt`

## Passing proof

`proof/SB14/transcripts/passing.txt`

## Source assertions

`proof/SB14/transcripts/source-assertions.txt`

## Anti-stub audit

`proof/SB14/transcripts/anti-stub-audit.txt`

## Changed-file hashes

`proof/SB14/transcripts/changed-file-hashes.txt`
