# SB04 Semantic Invariants

## Invariant SB04-KEY-COVERAGE-01

- Invariant ID: `SB04-KEY-COVERAGE-01`
- Source raw note: Cluster-key coverage metadata must be stored and used, not inferred from any two matching members.
- Expected behavior: Shared cluster keys expose member coverage counts/ratios and low-coverage keys are not promoted as representative cluster keys.
- Disallowed shallow implementation: Keeping a key because any two records share it in a larger cluster, or hiding coverage only in diagnostic text.
- Failing-first test: `SemanticInvariant_ClusterKeysExcludeSignalsBelowCoverageThreshold` in `bundle://proof/SB02/transcripts/failing-first-semantic-corpus.txt`.
- Passing test: `SemanticInvariant_ClusterKeysExcludeSignalsBelowCoverageThreshold` in `bundle://proof/SB04/transcripts/passing-semantic-tests.txt`.
- Changed source files: `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryQualityAlgorithmOptions.cs`, `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryQualityContracts.cs`, `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryClusterFormation.cs`, `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryClusterPlanner.cs`, and `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryQualityFoundationTests.cs`.
- Production assertions: `bundle://proof/SB04/transcripts/source-assertions.txt` proves configurable representative key coverage, key support counts/ratios, low-coverage key exclusion, primary-key coverage metrics, and pair-local coverage warnings; `bundle://proof/SB04/transcripts/no-migration-proof.txt` proves no EF schema files changed.
- Red-team negative case: A four-member release checklist cluster must not expose the two-member `cipher` signal as a representative key.
- Downstream dependency check: SB05 dream grouping must use coverage-aware cluster keys so unrelated claims are not grouped under weak keys.
