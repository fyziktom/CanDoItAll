# SB03 Semantic Invariants

## Invariant SB03-CROSS-PROJECT-01

- Invariant ID: `SB03-CROSS-PROJECT-01`
- Source raw note: `CrossProjectWeekly` must be truly cross-project while respecting policy boundaries.
- Expected behavior: Cross-project weekly dreaming can form an aggregate from readable memories in different projects and excludes restricted source text when policy does not allow it.
- Disallowed shallow implementation: Labeling same-project clusters as cross-project, or widening project scope without access-policy filtering.
- Failing-first test: `SemanticInvariant_CrossProjectWeeklyFormsOnlyPolicyAllowedCrossProjectClusters` in `bundle://proof/SB02/transcripts/failing-first-semantic-corpus.txt`.
- Passing test: `SemanticInvariant_CrossProjectWeeklyFormsOnlyPolicyAllowedCrossProjectClusters` and `SemanticInvariant_CrossProjectPlanningReportsPolicyBlockedPairsWithoutRestrictedMembers` in `bundle://proof/SB03/transcripts/passing-semantic-tests.txt`.
- Changed source files: `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryQualityContracts.cs`, `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryClusterFormation.cs`, `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryClusterPlanner.cs`, `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamConsolidationService.cs`, and `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryQualityFoundationTests.cs`.
- Production assertions: `bundle://proof/SB03/transcripts/source-assertions.txt` proves explicit `CognitiveMemoryClusterPlanningScope`, `PolicyConstrainedCrossProject`, scoped candidate-pair selection, policy-blocked pair metrics, member project provenance, and weekly dream selection by multiple readable source projects.
- Red-team negative case: Restricted cross-project source text containing `SECRET_TOKEN` must not appear in aggregate claims or source maps.
- Downstream dependency check: SB09 recall lineage must preserve cross-project source maps without leaking restricted content.

## Invariant SB03-APPROX-CANDIDATES-02

- Invariant ID: `SB03-APPROX-CANDIDATES-02`
- Source raw note: Approximate semantic candidate discovery must work beyond exact shared keys.
- Expected behavior: Paraphrased memories with no exact topic/entity key can still be compared within a bounded candidate budget and clustered when semantically related.
- Disallowed shallow implementation: Only adding fallback inside over-fanout exact-key groups, or comparing all records without a budget.
- Failing-first test: `SemanticInvariant_ApproximateCandidateDiscoveryPairsParaphrasesWithoutExactSharedKeys` in `bundle://proof/SB02/transcripts/failing-first-semantic-corpus.txt`.
- Passing test: `SemanticInvariant_ApproximateCandidateDiscoveryPairsParaphrasesWithoutExactSharedKeys` in `bundle://proof/SB03/transcripts/passing-semantic-tests.txt`.
- Changed source files: `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryQualityContracts.cs`, `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryClusterFormation.cs`, `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryClusterPlanner.cs`, and `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryQualityFoundationTests.cs`.
- Production assertions: `bundle://proof/SB03/transcripts/source-assertions.txt` proves bounded `AddApproximateSemanticPairs`, deterministic semantic alias signals, approximate-pair metrics, and content-edge scoring that does not depend on over-fanout exact-key fallback.
- Red-team negative case: Approximate discovery must not merge unrelated records that share only project scope, source topology, time, or access-risk keys.
- Downstream dependency check: SB04 and SB05 depend on richer candidate selection to avoid sparse or unrelated aggregate inputs.
