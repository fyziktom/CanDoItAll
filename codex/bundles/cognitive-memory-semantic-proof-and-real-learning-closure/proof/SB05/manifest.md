# SB05 Proof Manifest

## Subbundle

- Subbundle: 05-accepted-use-outcome-event-integration
- Status: Completed
- Owned requirements: R05 production accepted-use outcome integration.
- Test name: `SemanticInvariant_AcceptedUseSignalHasProductionOutcomeEventHandlerAndScheduledAssimilation`
- Test name: `AcceptedUseOutcomeEventHandler_EmitsAcceptedUseSignalIdempotentlyAndRejectsBroadLineage`

## Changed Files And Hashes

| File | SHA-256 |
|---|---:|
| `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryAdvancedContracts.cs` | `A1FF6362E613FFF7D519FD21AF924AD86252F4AED4C756F411767EE95B004FE0` |
| `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryProfessorAcceptedUseSignalEmitter.cs` | `7D5880A0937EDEEC1916B4A5BC03804E276738D53694486951AA16793704091F` |
| `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryRecallOutcomeAcceptedEventHandler.cs` | `F27C1A651F2077718FF4F4F8AE3EE3B7AD949C3018299C90C1DA8694C5B830AC` |
| `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryProfessorAssimilationEvaluator.cs` | `B9A7D8501E009F5D543276CA83A7F9F483EED8C5AB4CA7C34D355CE235BBD334` |
| `repo://src/CanDoItAll.Modules.CognitiveMemory/CognitiveMemoryModuleServiceCollectionExtensions.cs` | `2A6D38387C12BF5F3D606E229F8F46136DE862CC28E48546FAE92C01417288C3` |
| `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryAdvancedServicesTests.cs` | `7CD57001F5EF077151B02AA81909E861FA43EBE3E9D008C883707D9D90A33F2E` |

## Proof Artifacts

- Semantic invariant contract: bundle://proof/SB05/semantic-invariants.md
- Failing-first transcript: bundle://proof/SB05/transcripts/failing-first.txt
- Passing transcript: bundle://proof/SB05/transcripts/passing.txt
- Source assertion transcript: bundle://proof/SB05/transcripts/source-assertions.txt
- Anti-stub audit transcript: bundle://proof/SB05/transcripts/anti-stub.txt
- Source assertion: bundle://proof/SB05/transcripts/source-assertions.txt

## Proof Claim To Code Matrix

| Capability claim | Required production source proof | Required test proof | Required negative fixture | Result |
|---|---|---|---|---|
| automatic | repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryRecallOutcomeAcceptedEventHandler.cs and repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryProfessorAcceptedUseSignalEmitter.cs | bundle://proof/SB05/transcripts/passing.txt | failing negative rejected by bundle://proof/SB05/transcripts/failing-first.txt | Verified pass |
| scheduled | repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryAdvancedContracts.cs and repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryProfessorAnchorService.cs and repo://src/CanDoItAll.Modules.CognitiveMemory/CognitiveMemoryModuleServiceCollectionExtensions.cs | bundle://proof/SB05/transcripts/passing.txt | failing negative rejected by bundle://proof/SB05/transcripts/failing-first.txt | Verified pass |
| line-level | repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryRecallBriefComposition.cs and repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryRecallSynthesisService.cs and repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryReferenceResolver.cs | bundle://proof/SB05/transcripts/passing.txt | failing negative rejected by bundle://proof/SB05/transcripts/failing-first.txt | Verified pass |
## Closure

- Failing-first: bundle://proof/SB05/transcripts/failing-first.txt.
- Semantic positive proof: bundle://proof/SB05/transcripts/passing.txt.
- Source assertions: bundle://proof/SB05/transcripts/source-assertions.txt.
- Anti-stub audit: bundle://proof/SB05/transcripts/anti-stub.txt states no stubs were found.



