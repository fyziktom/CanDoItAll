# SB03 Proof Manifest

## Subbundle

- Subbundle: 03-failing-first-regression-corpus-current-gaps
- Status: Completed
- Owned requirements: R03 failing-first semantic regression corpus.
- Test name: `SemanticInvariant_CuratorCaptureCzechProfessorTeachingWithoutEnglishKeywordsPreservesDiacritics`
- Test name: `SemanticInvariant_AcceptedUseSignalHasProductionOutcomeEventHandlerAndScheduledAssimilation`
- Test name: `SemanticInvariant_ClusterDiscoveryUsesRealEmbeddingProviderAndHonestLexicalFallback`
- Test name: `DreamRun_CanonicalAggregateMemoryContainsDomainKnowledgeWithoutDiagnosticBoilerplate`
- Test name: `SemanticInvariant_DreamRunUsesClaimEvidenceLinksInsteadOfRecordWideSourceMaps`
- Test name: `SemanticInvariant_RecallBriefKeepsSharedSourceLineageOnlyForTheStatementSupport`

## Changed Files And Hashes

| File | SHA-256 |
|---|---:|
| `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryAdvancedServicesTests.cs` | `7CD57001F5EF077151B02AA81909E861FA43EBE3E9D008C883707D9D90A33F2E` |
| `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryQualityFoundationTests.cs` | `27856051661381AA5D341D51D3D8E4C7C1D2C810AE0C1829B2B3B6EF0BC16954` |

## Proof Artifacts

- Semantic invariant contract: bundle://proof/SB03/semantic-invariants.md
- Failing-first transcript: bundle://proof/SB03/transcripts/failing-first.txt
- Passing transcript: bundle://proof/SB03/transcripts/passing.txt
- Source assertion transcript: bundle://proof/SB03/transcripts/source-assertions.txt
- Anti-stub audit transcript: bundle://proof/SB03/transcripts/anti-stub.txt
- Source assertion: bundle://proof/SB03/transcripts/source-assertions.txt

## Proof Claim To Code Matrix

| Capability claim | Required production source proof | Required test proof | Required negative fixture | Result |
|---|---|---|---|---|
| Czech/diacritic | repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryProfessorAnchorExtraction.cs | bundle://proof/SB03/transcripts/passing.txt | failing negative rejected by bundle://proof/SB03/transcripts/failing-first.txt | Verified pass |
| embedding-backed | repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryClusterFormation.cs and repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryClusterPlanner.cs | bundle://proof/SB03/transcripts/passing.txt | failing negative rejected by bundle://proof/SB03/transcripts/failing-first.txt | Verified pass |
| provider-backed | repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryClusterFormation.cs and repo://src/CanDoItAll.Modules.CognitiveMemory/CognitiveMemoryModuleServiceCollectionExtensions.cs | bundle://proof/SB03/transcripts/passing.txt | failing negative rejected by bundle://proof/SB03/transcripts/failing-first.txt | Verified pass |
| automatic | repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryRecallOutcomeAcceptedEventHandler.cs and repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryProfessorAcceptedUseSignalEmitter.cs | bundle://proof/SB03/transcripts/passing.txt | failing negative rejected by bundle://proof/SB03/transcripts/failing-first.txt | Verified pass |
| scheduled | repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryAdvancedContracts.cs and repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryProfessorAnchorService.cs and repo://src/CanDoItAll.Modules.CognitiveMemory/CognitiveMemoryModuleServiceCollectionExtensions.cs | bundle://proof/SB03/transcripts/passing.txt | failing negative rejected by bundle://proof/SB03/transcripts/failing-first.txt | Verified pass |
| claim-specific | repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryQualitySupport.cs and repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamConsolidationService.cs | bundle://proof/SB03/transcripts/passing.txt | failing negative rejected by bundle://proof/SB03/transcripts/failing-first.txt | Verified pass |
| line-level | repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryRecallBriefComposition.cs and repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryRecallSynthesisService.cs and repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryReferenceResolver.cs | bundle://proof/SB03/transcripts/passing.txt | failing negative rejected by bundle://proof/SB03/transcripts/failing-first.txt | Verified pass |
| domain synthesis | repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamSynthesis.cs and repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamConsolidationService.cs and repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryQualityContracts.cs | bundle://proof/SB03/transcripts/passing.txt | failing negative rejected by bundle://proof/SB03/transcripts/failing-first.txt | Verified pass |
## Closure

- Failing-first: bundle://proof/SB03/transcripts/failing-first.txt.
- Semantic positive proof: bundle://proof/SB03/transcripts/passing.txt.
- Source assertions: bundle://proof/SB03/transcripts/source-assertions.txt.
- Anti-stub audit: bundle://proof/SB03/transcripts/anti-stub.txt states no stubs were found.



