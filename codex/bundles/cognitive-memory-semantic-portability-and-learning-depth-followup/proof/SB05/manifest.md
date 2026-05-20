# SB05 Proof Manifest

## Status

- Subbundle: `SB05 - Claim-aware Dream Grouping And Structured Synthesis`
- Status: `Completed`
- Owned requirements: `R-06`, `R-07`, `R-16`
- Raw notes: dream grouping must separate unrelated claims that share a cluster key, and dream synthesis must preserve claim structure instead of concatenating source text.
- Semantic invariant contract: `bundle://proof/SB05/semantic-invariants.md`

## Changed File Hashes

Complete after-change SHA-256 values are recorded in `bundle://proof/SB05/transcripts/changed-file-hashes.txt`.

Primary after-change SHA-256 values:

- `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamConsolidationService.cs`
  - SHA-256: `3acc48d889f995aed662bd11aacc26a2f563b6af4f6ba155ed06aa7ad7151b78`
- `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamSynthesis.cs`
  - SHA-256: `e40b53eaa4a78d4434f05a5a7ded9303ebc58a98da4b0ccad6cf172e01f8aeec`
- `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryQualityFoundationTests.cs`
  - SHA-256: `d1b30ba471d926802db0ff78b26a406e806ba8a4bfdf54bf1e2436522b25091f`
- `bundle://proof/SB05/semantic-invariants.md`
  - SHA-256: `d5d1fbb888f169fe16608b597b17131833f6ff313dd0bda9bfd74117cb85ad28`

## Command Transcripts

- Failing-first transcript: `bundle://proof/SB02/transcripts/failing-first-semantic-corpus.txt`
- Passing transcript: `bundle://proof/SB05/transcripts/passing-semantic-tests.txt`
- Regression transcript: `bundle://proof/SB05/transcripts/regression-tests.txt`
- Source assertions transcript: `bundle://proof/SB05/transcripts/source-assertions.txt`
- No-migration proof transcript: `bundle://proof/SB05/transcripts/no-migration-proof.txt`
- Anti-stub audit transcript: `bundle://proof/SB05/transcripts/anti-stub-audit.txt`
- Hash transcript: `bundle://proof/SB05/transcripts/changed-file-hashes.txt`
- Prepared validator transcript: `bundle://proof/SB05/transcripts/prepared-validator-after-sb05.txt`

## Tests And Invariants

- Test name: `SemanticInvariant_DreamRunSeparatesUnrelatedClaimsSharingPrimaryClusterKey`
- Test name: `SemanticInvariant_DreamClaimSynthesisProducesStructuredSlots`
- Test name: `DreamRun_IntegratesComplementaryProcedureClaimsIntoSingleAggregateStatement`
- Test name: `DreamRun_ProjectNightlyCreatesApprovedCandidateAndMetrics`
- Test name: `DreamRun_ProducesModeSpecificStructuredOutputsBeyondTitlePrefix`
- Test name: `SemanticInvariant_CrossProjectWeeklyFormsOnlyPolicyAllowedCrossProjectClusters`
- Test name: `SemanticInvariant_ApproximateCandidateDiscoveryPairsParaphrasesWithoutExactSharedKeys`
- Test name: `SemanticInvariant_ClusterKeysExcludeSignalsBelowCoverageThreshold`

Invariant IDs covered by transcripts:

- `SB05-CLAIM-GROUPING-01`
- `SB05-STRUCTURED-SYNTHESIS-02`

## Source Assertions

`bundle://proof/SB05/transcripts/source-assertions.txt` proves the implementation exposes claim slots, deterministic slot extraction, claim signatures, representative slots, complementary-claim grouping for procedure-style subjects, and structured synthesis sections for conclusion, support, condition, and caveat.

## Red-Team Negative Proof

`bundle://proof/SB05/transcripts/passing-semantic-tests.txt` proves tenant-data export and payment-batch export facts remain in separate aggregate claims even when they share `project.operations.policy` as the primary cluster key. The same transcript proves complementary payment export procedure facts still group into one aggregate statement instead of over-splitting all related procedure claims.

## Browser And Host Proof

Browser validation: N/A. SB05 changes backend dream grouping/synthesis services and unit tests only; no UI routes, components, host startup behavior, or browser-visible behavior changed.

## Persistence And Migration Proof

`bundle://proof/SB05/transcripts/no-migration-proof.txt` proves no EF entity, DbContext, model snapshot, SQLite migration, or PostgreSQL migration files changed. SB05 stores the new claim slots and signatures in transient dream grouping flow and existing claim contracts without changing persisted schema.

## Downstream Dependency Check

`bundle://proof/SB05/transcripts/regression-tests.txt` reruns upstream SB03/SB04 semantic tests plus ProjectNightly and mode-specific dream regressions. `bundle://proof/SB05/transcripts/prepared-validator-after-sb05.txt` proves the bundle remains valid for prepared-stage progression after SB05 closure. SB06 entailment can now validate claim groups without inheriting mode-plus-primary-key merges, and SB09 recall lineage can cite source maps per synthesized group.
