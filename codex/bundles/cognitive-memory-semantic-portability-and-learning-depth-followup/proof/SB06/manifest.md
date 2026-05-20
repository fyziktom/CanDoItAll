# SB06 Proof Manifest

## Status

- Subbundle: `SB06 - Deep Entailment, Contradiction, And Calibrated Apply`
- Status: `Completed`
- Owned requirements: `R-08`, `R-16`
- Raw notes: dream validation must go beyond lexical overlap, and aggregate application must avoid overconfident acceptance for complex or weakly validated claims.
- Semantic invariant contract: `bundle://proof/SB06/semantic-invariants.md`

## Changed File Hashes

Complete after-change SHA-256 values are recorded in `bundle://proof/SB06/transcripts/changed-file-hashes.txt`.

Primary after-change SHA-256 values:

- `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamSynthesis.cs`
  - SHA-256: `bd47a3c982f3daed97800da03edafb32b15ecb2ef0e784ab81a12767ca6119e1`
- `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamValidator.cs`
  - SHA-256: `638cba21a4d9bc7fdfc70ea105514251a6422b9e6d49d6feefe1e752608875b5`
- `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryAggregateMemoryApplicator.cs`
  - SHA-256: `d85419dfdacb3249d6fbf6a8d930061e0d41c79d224a8511925f611e2b135e19`
- `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryAggregateConfidenceCalibrator.cs`
  - SHA-256: `bc883549359fd3e964521396ef54d07d45a568fd92d10dfaea212c58717f29ad`
- `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryQualityAlgorithmOptions.cs`
  - SHA-256: `4b828c7bc05cc4778b8d1ad5d9f202fa4cc0dcb8eb2a56d7a4e4f786d48dea9f`
- `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryQualityFoundationTests.cs`
  - SHA-256: `b2412faa53b55175a506279554a26db85c13433b6ea5eeb1861fc767e9a58a6c`
- `bundle://proof/SB06/semantic-invariants.md`
  - SHA-256: `f65a996d86c0bb2fefe8433c5f3e36133271fef0b17c192422ea98e00c94a21c`

## Command Transcripts

- Failing-first transcript: `bundle://proof/SB02/transcripts/failing-first-semantic-corpus.txt`
- Passing transcript: `bundle://proof/SB06/transcripts/passing-semantic-tests.txt`
- Regression transcript: `bundle://proof/SB06/transcripts/regression-tests.txt`
- Source assertions transcript: `bundle://proof/SB06/transcripts/source-assertions.txt`
- No-migration proof transcript: `bundle://proof/SB06/transcripts/no-migration-proof.txt`
- Anti-stub audit transcript: `bundle://proof/SB06/transcripts/anti-stub-audit.txt`
- Hash transcript: `bundle://proof/SB06/transcripts/changed-file-hashes.txt`
- Prepared validator transcript: `bundle://proof/SB06/transcripts/prepared-validator-after-sb06.txt`

## Tests And Invariants

- Test name: `SemanticInvariant_DreamEntailmentRejectsNumericTemporalActorConditionalAndScopeReversals`
- Test name: `DreamEntailment_SupportsMatchingSemanticOperators`
- Test name: `DreamValidation_RoutesNumericReversalToReviewWithIssueReason`
- Test name: `DreamValidation_RoutesUnsupportedMappedClaimToReview`
- Test name: `DreamValidation_RejectsNegatedClaimDespiteHighTokenOverlap`
- Test name: `AggregateConfidenceCalibrator_DemotesOperatorBearingAggregateDespiteBroadEvidence`
- Test name: `DreamRun_ProjectNightlyCreatesApprovedCandidateAndMetrics`
- Test name: `DreamRun_IntegratesComplementaryProcedureClaimsIntoSingleAggregateStatement`
- Test name: `SemanticInvariant_DreamRunSeparatesUnrelatedClaimsSharingPrimaryClusterKey`
- Test name: `SemanticInvariant_ClusterKeysExcludeSignalsBelowCoverageThreshold`

Invariant IDs covered by transcripts:

- `SB06-DEEP-ENTAILMENT-01`

## Source Assertions

`bundle://proof/SB06/transcripts/source-assertions.txt` proves the implementation extracts semantic profiles, numeric measurements, temporal relations, actor/action roles, condition polarity, source-only scopes, and operator signal counts. It also proves validation issue messages include entailment blocker reasons and aggregate confidence calibration consumes validation depth, source maps, operator-bearing claim count, and claim complexity.

## Red-Team Negative Proof

`bundle://proof/SB06/transcripts/passing-semantic-tests.txt` proves six adversarial reversal families are rejected: numeric value mismatch, temporal before/after reversal, actor/object reversal, condition pass/fail reversal, optional/required reversal, and production/test scope reversal. It also proves a persisted dream candidate with an unsupported numeric reversal routes to `NeedsHumanReview` with a claim-level numeric issue reason.

## Browser And Host Proof

Browser validation: N/A. SB06 changes backend dream validation, confidence calibration, and unit tests only; no UI routes, components, host startup behavior, or browser-visible behavior changed.

## Persistence And Migration Proof

`bundle://proof/SB06/transcripts/no-migration-proof.txt` proves no EF entity, DbContext, model snapshot, SQLite migration, or PostgreSQL migration files changed. SB06 records deeper validation explanations through the existing `IssuesJson` validation payload and uses existing confidence/stability fields.

## Downstream Dependency Check

`bundle://proof/SB06/transcripts/regression-tests.txt` reruns SB03-SB05 semantic tests plus dream validation, ProjectNightly, and confidence calibration regressions. `bundle://proof/SB06/transcripts/prepared-validator-after-sb06.txt` proves the bundle remains valid for prepared-stage progression after SB06 closure. SB08 assimilation can now reject aggregates that only passed lexical overlap, and SB09 recall can depend on claim-level validation reasons.
