# SB02 Proof Manifest

## Status

- Subbundle: `SB02 - Failing-first regression corpus for current gaps`
- Status: `Completed`
- Owned requirements: `R02`
- Raw notes: Add failing-first tests for remaining production behavior gaps before changing production code.
- Semantic invariant contract: `bundle://proof/SB02/semantic-invariants.md`

## Changed File Hashes

| File | SHA-256 |
|---|---:|
| `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryAdvancedServicesTests.cs` | `D790886E2DD65ADF2C7D4B75FB63435EFC2D59F4EE088FFA901EFBA94D536B27` |
| `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryQualityFoundationTests.cs` | `122C91531BF0286CE3184611E53D81AA89D923600E83AF12746C407E13690D4D` |

Full hash transcript: `bundle://proof/SB02/transcripts/changed-file-hashes.txt`.

## Command Transcripts

- Failing-first transcript: `bundle://proof/SB02/transcripts/failing-first.txt`
- Passing transcript: `bundle://proof/SB02/transcripts/passing.txt`
- Source assertions transcript: `bundle://proof/SB02/transcripts/source-assertions.txt`
- Anti-stub audit transcript: `bundle://proof/SB02/transcripts/anti-stub.txt`
- Changed-file hashes: `bundle://proof/SB02/transcripts/changed-file-hashes.txt`

## Tests And Invariants

- Test name: `SemanticInvariant_AcceptedUseSignalHasProductionEmitterAndScheduledAssimilation`
- Test name: `SemanticInvariant_ProfessorComparisonReviewResolutionIsExplicitAndAudited`
- Test name: `SemanticInvariant_CuratorCaptureCzechDiacriticsAndNaturalScopeCreatesProfessorAnchor`
- Test name: `DreamRun_CanonicalAggregateMemoryContainsDomainKnowledgeWithoutDiagnosticBoilerplate`
- Test name: `SemanticInvariant_DreamConsolidationCreatesClaimSpecificSourceMaps`
- Test name: `SemanticInvariant_ClusterDiscoveryHasEmbeddingBackedApproximateCandidateProvider`
- Test name: `SemanticInvariant_RecallSynthesisRequestCarriesRealQueryIntentAndLineage`
- Invariant ID: `SB02-ACCEPTED-USE-01`
- Invariant ID: `SB02-COMPARISON-REVIEW-02`
- Invariant ID: `SB02-MULTILINGUAL-CAPTURE-03`
- Invariant ID: `SB02-DREAM-PROVENANCE-04`
- Invariant ID: `SB02-SEMANTIC-CLUSTERING-05`
- Invariant ID: `SB02-RECALL-LINEAGE-06`
- Invariant ID: `SB02-NO-PRODUCTION-DIFF-07`

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
|---|---|---|---|---|
| `ProfessorAnchorAcceptedUse` future producer | `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryAdvancedServicesTests.cs` asserts `ICognitiveMemoryProfessorAcceptedUseSignalEmitter` and `CognitiveMemoryProfessorAcceptedUseSignalRequest` | `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryAdvancedServicesTests.cs` asserts `SignalKind = CognitiveMemorySignalKind.ProfessorAnchorAcceptedUse` is emitted from production code | `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryAdvancedServicesTests.cs` asserts scheduled automation calls `ScanAssimilationAsync` | `bundle://proof/SB02/transcripts/failing-first.txt` proves the current consumer-only implementation fails |

## Source Assertions

`bundle://proof/SB02/transcripts/source-assertions.txt` proves the new tests are confined to `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryAdvancedServicesTests.cs` and `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryQualityFoundationTests.cs`. `bundle://proof/SB02/transcripts/passing.txt` records that `repo://src/CanDoItAll.Modules.CognitiveMemory` has no production diff for SB02.

## Red-Team Negative Proof

`bundle://proof/SB02/transcripts/failing-first.txt` records a non-zero targeted test run with seven failing tests. The failures cover accepted-use producer absence, missing scheduled assimilation wiring, missing comparison review resolution API/audit path, Czech diacritics capture miss, dream meta-text storage, missing claim-specific source-map boundary, missing embedding-backed approximate provider, and missing real-query recall synthesis contract.

## Browser And Host Proof

Browser validation: N/A. SB02 adds backend unit tests and proof artifacts only; no UI routes, components, host startup behavior, or OS shell behavior changed.

## Downstream Dependency Check

SB03-SB08 must cite these red-baseline tests and make them pass through production changes. SB02 intentionally does not change production cognitive-memory code; `bundle://proof/SB02/transcripts/passing.txt` records the no-production-diff check.
