# SB03 Proof Manifest

## Status

- Subbundle: `SB03 - Cross-project And Approximate Candidate Discovery`
- Status: `Completed`
- Owned requirements: `R-03`, `R-04`, `R-16`
- Raw notes: cross-project weekly clustering must be real, policy constrained, and supported by bounded approximate candidate discovery beyond exact shared keys.
- Semantic invariant contract: `bundle://proof/SB03/semantic-invariants.md`

## Changed File Hashes

Complete after-change SHA-256 values are recorded in `bundle://proof/SB03/transcripts/changed-file-hashes.txt`.

Primary after-change SHA-256 values:

- `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryQualityContracts.cs`
  - SHA-256: `1e7ddba3f28ca13487162d6c3fe957c42a79b237a30974d56987e85ae8599d2f`
- `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryClusterFormation.cs`
  - SHA-256: `c9df23aef58b3bac70f269cdc9eff14d2596703b477a338eb4673984a51f884a`
- `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryClusterPlanner.cs`
  - SHA-256: `383dc4214a449c61499c4ed6d6fe196f48102278489bd3a372cc0d8bd35e5054`
- `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamConsolidationService.cs`
  - SHA-256: `1510af9ab849100e3692ace23aa481a8be72520263b84b8e4bd5b8ab6a3e3060`
- `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryQualityFoundationTests.cs`
  - SHA-256: `be1b28d6aeaabbad773d01171ac5e3c4c07965ca8dcef9de795f573b7cf8ad61`
- `bundle://proof/SB03/semantic-invariants.md`
  - SHA-256: `42e87a8ebf995869682b425752b00a7fa72eaaf6e7a2c220b3baafe02d6a155c`

## Command Transcripts

- Failing-first transcript: `bundle://proof/SB02/transcripts/failing-first-semantic-corpus.txt`
- Passing transcript: `bundle://proof/SB03/transcripts/passing-semantic-tests.txt`
- Regression transcript: `bundle://proof/SB03/transcripts/regression-tests.txt`
- Source assertions transcript: `bundle://proof/SB03/transcripts/source-assertions.txt`
- Anti-stub audit transcript: `bundle://proof/SB03/transcripts/anti-stub-audit.txt`
- Hash transcript: `bundle://proof/SB03/transcripts/changed-file-hashes.txt`
- Prepared validator transcript: `bundle://proof/SB03/transcripts/prepared-validator-after-sb03.txt`

## Tests And Invariants

- Test name: `SemanticInvariant_CrossProjectWeeklyFormsOnlyPolicyAllowedCrossProjectClusters`
- Test name: `SemanticInvariant_ApproximateCandidateDiscoveryPairsParaphrasesWithoutExactSharedKeys`
- Test name: `SemanticInvariant_CrossProjectPlanningReportsPolicyBlockedPairsWithoutRestrictedMembers`
- Test name: `ClusterPlanner_PersistsCompositeClustersWithSupportingKeyFamilies`
- Test name: `ClusterPlanner_MergesRelatedMemoriesAcrossDifferentTitlesAndTopicKeys`
- Test name: `ClusterPlanner_UsesHighFanoutFallbackForParaphrasedSemanticPair`
- Test name: `DreamRun_ProjectNightlyCreatesApprovedCandidateAndMetrics`
- Test name: `ReferenceResolver_DeniesRestrictedReferenceWithoutLocatorOrSummary`

Invariant IDs covered by transcripts:

- `SB03-CROSS-PROJECT-01`
- `SB03-APPROX-CANDIDATES-02`

## Source Assertions

`bundle://proof/SB03/transcripts/source-assertions.txt` proves the production implementation contains explicit `CognitiveMemoryClusterPlanningScope`, `PolicyConstrainedCrossProject`, scoped `SelectCandidatePairs`, bounded `AddApproximateSemanticPairs`, semantic alias signals, policy-blocked pair metrics, member project provenance, and dream weekly selection by multiple readable source projects.

## Red-Team Negative Proof

`bundle://proof/SB03/transcripts/passing-semantic-tests.txt` includes the restricted cross-project negative path through `SemanticInvariant_CrossProjectPlanningReportsPolicyBlockedPairsWithoutRestrictedMembers`: restricted records are excluded from cluster members and `PolicyBlockedCandidatePairs` is reported. The same transcript covers `SemanticInvariant_CrossProjectWeeklyFormsOnlyPolicyAllowedCrossProjectClusters`, which rejects source-map leakage of `SECRET_TOKEN`.

## Browser And Host Proof

Browser validation: N/A. SB03 changes backend cognitive-memory clustering, dream selection, and unit tests only; no UI routes, components, host startup behavior, or browser-visible behavior changed.

## Downstream Dependency Check

`bundle://proof/SB03/transcripts/regression-tests.txt` retests ProjectNightly dream behavior, persisted composite clusters, exact-key semantic clustering, high-fanout fallback, and restricted reference denial. `bundle://proof/SB03/transcripts/prepared-validator-after-sb03.txt` proves the bundle remains valid for prepared-stage progression after SB03 closure. SB04 and SB05 can now rely on richer cross-project and approximate candidate inputs without weakening access/redaction guards.
