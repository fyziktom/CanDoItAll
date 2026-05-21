# SB06 Semantic Invariants

## Invariant SB06-EMBEDDING-CLUSTER-01

- Invariant ID: SB06-EMBEDDING-CLUSTER-01
- Source raw note: R06 embedding-backed approximate cluster discovery.
- Expected behavior: Approximate clustering uses ICognitiveMemoryEmbeddingProvider vectors for provider-backed semantic similarity, keeps an honest lexical fallback, and can cluster paraphrases with no shared signals.
- Disallowed shallow implementation: A class named embedding-backed that only compares lexical rare signals would satisfy names while still shipping lexical behavior.
- Failing-first test: bundle://proof/SB06/transcripts/failing-first.txt.
- Passing test: bundle://proof/SB06/transcripts/passing.txt.
- Changed source files: `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryClusterFormation.cs` hash `0010D63663A3A47DF9837D21C54964BE0071501EC05E1FD9AB051D85735472BB`; `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryClusterPlanner.cs` hash `717255C67971B4D1503E34D7A523A17567DBF3A55A5C190C0C3CA5F48CD488C6`; `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryQualityAlgorithmOptions.cs` hash `84FBD48280E7375461AD33CF4357DC0825A2FEFFF6A5C5D06B87CFBC93E1E7EF`; `repo://src/CanDoItAll.Modules.CognitiveMemory/CognitiveMemoryModuleServiceCollectionExtensions.cs` hash `2A6D38387C12BF5F3D606E229F8F46136DE862CC28E48546FAE92C01417288C3`; `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryQualityFoundationTests.cs` hash `27856051661381AA5D341D51D3D8E4C7C1D2C810AE0C1829B2B3B6EF0BC16954`.
- Production assertions: bundle://proof/SB06/transcripts/source-assertions.txt cites the production paths that implement the invariant.
- Red-team negative case: bundle://proof/SB06/transcripts/failing-first.txt records the failing shallow case for the invariant.
- Downstream dependency check: SB06 proof is included in bundle://reviews/01-execution-report.md, the cognitive-memory focused tests, and the completed-stage bundle validation.


