# SB07 Proof Manifest

## Status

- Subbundle: `SB07 - Embedding-backed approximate cluster discovery`
- Status: `Completed`
- Owned requirements: `R07`
- Raw notes: Approximate clustering needs a production provider boundary with continuation diagnostics and metrics.
- Semantic invariant contract: `bundle://proof/SB07/semantic-invariants.md`

## Changed File Hashes

| File | SHA-256 |
|---|---:|
| `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryClusterFormation.cs` | `4BCB4A5B2D5550F2A79FF513B022DDA895FACE0F5E6C06847D3F279116FDF79C` |
| `repo://src/CanDoItAll.Modules.CognitiveMemory/CognitiveMemoryModuleServiceCollectionExtensions.cs` | `7EFA7C5BD3BC417FF1B29983B6930468EB22D19536C13E7298CFC50803A2C2E7` |
| `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryQualityFoundationTests.cs` | `82BB9D0116260EB97EE7AB9DB6ABC501E99D792654970C20672B69AA70D6D027` |
| `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryModuleRegistrationTests.cs` | `552E9034C5DFC2DE82172DDDE51972DE44068F7C34F7ED52701016D55413174E` |

Full hash transcript: `bundle://proof/SB07/transcripts/changed-file-hashes.txt`.

## Command Transcripts

- Failing-first transcript: `bundle://proof/SB07/transcripts/failing-first.txt`
- Passing transcript: `bundle://proof/SB07/transcripts/passing.txt`
- Source assertions transcript: `bundle://proof/SB07/transcripts/source-assertions.txt`
- Anti-stub audit transcript: `bundle://proof/SB07/transcripts/anti-stub.txt`
- Changed-file hashes: `bundle://proof/SB07/transcripts/changed-file-hashes.txt`

## Tests And Invariants

- Test name: `SemanticInvariant_ClusterDiscoveryHasEmbeddingBackedApproximateCandidateProvider`
- Test name: `CognitiveMemoryModule_RegistersQualityCollaboratorsAndVersionedOptions`
- Invariant ID: `SB07-APPROXIMATE-PROVIDER-01`
- Invariant ID: `SB07-DI-REGISTRATION-02`

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
|---|---|---|---|---|
| `ICognitiveMemoryApproximateClusterCandidateProvider` | `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryClusterFormation.cs` implements `CognitiveMemoryEmbeddingBackedApproximateClusterCandidateProvider` | `CognitiveMemoryCandidatePairSelector` consumes the provider for fanout and semantic candidate pairs | DI registers the provider and injects it into the selector | SB02 red baseline proves the provider boundary was absent |
| `ApproximateCandidatePairsGenerated` metric | Provider result exposes generated approximate count | Selector accumulator reports approximate counts through existing selection/result metrics | Cluster planner surfaces existing approximate-pair metrics | Source assertion requires metric presence and continuation cursor contract |

## Source Assertions

`bundle://proof/SB07/transcripts/source-assertions.txt` records provider request/result contracts, `ContinuationCursor`, `EmbeddingProfileId`, DI registration, and selector consumption.

## Red-Team Negative Proof

The provider enforces same-project filtering unless cross-project scope allows it, deduplicates pairs, applies semantic thresholding, and respects pair budget.

## Browser And Host Proof

Browser validation: N/A. SB07 is backend quality clustering logic.

## Downstream Dependency Check

SB09 and SB10 can reason about clustering through a named provider boundary instead of burying approximate logic in the selector.
