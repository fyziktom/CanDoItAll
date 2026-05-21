# SB06 Proof Manifest

## Subbundle

- Subbundle: 06-real-embedding-ranker-cluster-discovery
- Status: Completed
- Owned requirements: R06 embedding-backed approximate cluster discovery.
- Test name: `SemanticInvariant_ClusterDiscoveryUsesRealEmbeddingProviderAndHonestLexicalFallback`
- Test name: `SemanticInvariant_EmbeddingCandidateDiscoveryPairsParaphrasesWithoutSharedSignals`

## Changed Files And Hashes

| File | SHA-256 |
|---|---:|
| `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryClusterFormation.cs` | `0010D63663A3A47DF9837D21C54964BE0071501EC05E1FD9AB051D85735472BB` |
| `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryClusterPlanner.cs` | `717255C67971B4D1503E34D7A523A17567DBF3A55A5C190C0C3CA5F48CD488C6` |
| `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryQualityAlgorithmOptions.cs` | `84FBD48280E7375461AD33CF4357DC0825A2FEFFF6A5C5D06B87CFBC93E1E7EF` |
| `repo://src/CanDoItAll.Modules.CognitiveMemory/CognitiveMemoryModuleServiceCollectionExtensions.cs` | `2A6D38387C12BF5F3D606E229F8F46136DE862CC28E48546FAE92C01417288C3` |
| `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryQualityFoundationTests.cs` | `27856051661381AA5D341D51D3D8E4C7C1D2C810AE0C1829B2B3B6EF0BC16954` |

## Proof Artifacts

- Semantic invariant contract: bundle://proof/SB06/semantic-invariants.md
- Failing-first transcript: bundle://proof/SB06/transcripts/failing-first.txt
- Passing transcript: bundle://proof/SB06/transcripts/passing.txt
- Source assertion transcript: bundle://proof/SB06/transcripts/source-assertions.txt
- Anti-stub audit transcript: bundle://proof/SB06/transcripts/anti-stub.txt
- Source assertion: bundle://proof/SB06/transcripts/source-assertions.txt

## Proof Claim To Code Matrix

| Capability claim | Required production source proof | Required test proof | Required negative fixture | Result |
|---|---|---|---|---|
| embedding-backed | repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryClusterFormation.cs and repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryClusterPlanner.cs | bundle://proof/SB06/transcripts/passing.txt | failing negative rejected by bundle://proof/SB06/transcripts/failing-first.txt | Verified pass |
| provider-backed | repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryClusterFormation.cs and repo://src/CanDoItAll.Modules.CognitiveMemory/CognitiveMemoryModuleServiceCollectionExtensions.cs | bundle://proof/SB06/transcripts/passing.txt | failing negative rejected by bundle://proof/SB06/transcripts/failing-first.txt | Verified pass |
## Closure

- Failing-first: bundle://proof/SB06/transcripts/failing-first.txt.
- Semantic positive proof: bundle://proof/SB06/transcripts/passing.txt.
- Source assertions: bundle://proof/SB06/transcripts/source-assertions.txt.
- Anti-stub audit: bundle://proof/SB06/transcripts/anti-stub.txt states no stubs were found.



