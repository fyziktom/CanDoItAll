# SB04 Proof Manifest

## Status

- Subbundle: `SB04 - Coverage-aware Cluster Keys And Quality Metrics`
- Status: `Completed`
- Owned requirements: `R-05`, `R-14`, `R-16`
- Raw notes: cluster keys and primary keys must represent the cluster, not only a pair inside a larger cluster.
- Semantic invariant contract: `bundle://proof/SB04/semantic-invariants.md`

## Changed File Hashes

Complete after-change SHA-256 values are recorded in `bundle://proof/SB04/transcripts/changed-file-hashes.txt`.

Primary after-change SHA-256 values:

- `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryQualityAlgorithmOptions.cs`
  - SHA-256: `719e469947bad6ec515f663d4d5f64f311f7e0d8ac6f082247b2a5762b977f08`
- `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryQualityContracts.cs`
  - SHA-256: `ab1a03c0a9d6153464682d62577e9b58380e1844e0930ea2543955e205593427`
- `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryClusterFormation.cs`
  - SHA-256: `4e88308d5d1b8bb9c4e2513c18aaad9b48887a87be9b885fac756b4f78a2d0e0`
- `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryClusterPlanner.cs`
  - SHA-256: `b789335dfb0eb275a6714d7a0994fe57f6705abe2647513e05a10009fba1a308`
- `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryQualityFoundationTests.cs`
  - SHA-256: `d1b30ba471d926802db0ff78b26a406e806ba8a4bfdf54bf1e2436522b25091f`
- `bundle://proof/SB04/semantic-invariants.md`
  - SHA-256: `48e5dedf7cfb633f83119be89bf3490be2da583c3c5f0db9b92ca422ccb5fe4e`

## Command Transcripts

- Failing-first transcript: `bundle://proof/SB02/transcripts/failing-first-semantic-corpus.txt`
- Passing transcript: `bundle://proof/SB04/transcripts/passing-semantic-tests.txt`
- Regression transcript: `bundle://proof/SB04/transcripts/regression-tests.txt`
- Source assertions transcript: `bundle://proof/SB04/transcripts/source-assertions.txt`
- No-migration proof transcript: `bundle://proof/SB04/transcripts/no-migration-proof.txt`
- Anti-stub audit transcript: `bundle://proof/SB04/transcripts/anti-stub-audit.txt`
- Hash transcript: `bundle://proof/SB04/transcripts/changed-file-hashes.txt`
- Prepared validator transcript: `bundle://proof/SB04/transcripts/prepared-validator-after-sb04.txt`

## Tests And Invariants

- Test name: `SemanticInvariant_ClusterKeysExcludeSignalsBelowCoverageThreshold`
- Test name: `SemanticInvariant_CrossProjectWeeklyFormsOnlyPolicyAllowedCrossProjectClusters`
- Test name: `SemanticInvariant_ApproximateCandidateDiscoveryPairsParaphrasesWithoutExactSharedKeys`
- Test name: `SemanticInvariant_CrossProjectPlanningReportsPolicyBlockedPairsWithoutRestrictedMembers`
- Test name: `ClusterPlanner_PersistsCompositeClustersWithSupportingKeyFamilies`
- Test name: `ClusterPlanner_MergesRelatedMemoriesAcrossDifferentTitlesAndTopicKeys`
- Test name: `ClusterPlanner_UsesHighFanoutFallbackForParaphrasedSemanticPair`
- Test name: `DreamRun_ProjectNightlyCreatesApprovedCandidateAndMetrics`

Invariant IDs covered by transcripts:

- `SB04-KEY-COVERAGE-01`

## Source Assertions

`bundle://proof/SB04/transcripts/source-assertions.txt` proves the implementation exposes `SupportCount`, `CoverageRatio`, `PrimaryKeyCoverageRatio`, `LowCoverageKeyCount`, `MinimumRepresentativeKeyCoverageRatio`, representative key filtering, low-coverage key summaries, and pair-local warnings.

## Red-Team Negative Proof

`bundle://proof/SB04/transcripts/passing-semantic-tests.txt` proves the four-member release checklist cluster excludes the two-member `cipher` key from representative cluster keys while retaining high-coverage topic metadata and coverage diagnostics.

## Browser And Host Proof

Browser validation: N/A. SB04 changes backend clustering contracts/options/planner logic and unit tests only; no UI routes, components, host startup behavior, or browser-visible behavior changed.

## Persistence And Migration Proof

`bundle://proof/SB04/transcripts/no-migration-proof.txt` proves no EF entity/configuration or SQLite/PostgreSQL migration files changed. SB04 stores coverage metadata in runtime cluster key contracts and quality metrics without changing the persisted schema.

## Downstream Dependency Check

`bundle://proof/SB04/transcripts/regression-tests.txt` reruns SB03 semantic tests plus cluster, high-fanout, and ProjectNightly regressions. `bundle://proof/SB04/transcripts/prepared-validator-after-sb04.txt` proves the bundle remains valid for prepared-stage progression after SB04 closure. SB05 dream grouping can now consume representative keys without inheriting pair-local `cipher`-style keys.
