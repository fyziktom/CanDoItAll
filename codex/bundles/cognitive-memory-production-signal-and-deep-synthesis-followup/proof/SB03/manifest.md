# SB03 Proof Manifest

## Status

- Subbundle: `SB03 - Production accepted-use emitter and assimilation wiring`
- Status: `Completed`
- Owned requirements: `R03`
- Raw notes: Accepted-use evidence must have a production producer and assimilation must run from lifecycle flows.
- Semantic invariant contract: `bundle://proof/SB03/semantic-invariants.md`

## Changed File Hashes

| File | SHA-256 |
|---|---:|
| `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryAdvancedContracts.cs` | `A53AEC77C5C6463681DCDEE6C66694E7B473C314E625B517528810DBADE18FF8` |
| `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryProfessorAcceptedUseSignalEmitter.cs` | `4087F7CDF078DA71E46E9AB3579A06894041838B850DBE7A4A070D85F55A0E62` |
| `repo://src/CanDoItAll.Modules.CognitiveMemory/Operations/CognitiveMemoryScheduledAutomationRunner.cs` | `40C5D2BD16922578D7E1F84F8A3563E7368366A8B2CD358C91C16B6936EA45FC` |
| `repo://src/CanDoItAll.Modules.CognitiveMemory/CognitiveMemoryModuleServiceCollectionExtensions.cs` | `7EFA7C5BD3BC417FF1B29983B6930468EB22D19536C13E7298CFC50803A2C2E7` |
| `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryAdvancedServicesTests.cs` | `AA63276727CD6793DF76447A03F42E445431AB57774EF65D050B5D89231AE2CB` |

Full hash transcript: `bundle://proof/SB03/transcripts/changed-file-hashes.txt`.

## Command Transcripts

- Failing-first transcript: `bundle://proof/SB03/transcripts/failing-first.txt`
- Passing transcript: `bundle://proof/SB03/transcripts/passing.txt`
- Source assertions transcript: `bundle://proof/SB03/transcripts/source-assertions.txt`
- Anti-stub audit transcript: `bundle://proof/SB03/transcripts/anti-stub.txt`
- Changed-file hashes: `bundle://proof/SB03/transcripts/changed-file-hashes.txt`

## Tests And Invariants

- Test name: `SemanticInvariant_AcceptedUseSignalHasProductionEmitterAndScheduledAssimilation`
- Test name: `AcceptedUseEmitter_PublishesRecallTraceSignalAndRejectsDirectCaptureMemory`
- Invariant ID: `SB03-ACCEPTED-USE-PRODUCER-01`
- Invariant ID: `SB03-SCHEDULED-ASSIMILATION-02`

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
|---|---|---|---|---|
| `ProfessorAnchorAcceptedUse` signal | `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryProfessorAcceptedUseSignalEmitter.cs` publishes through `ICognitiveMemorySignalLedger` | `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryProfessorAssimilationEvaluator.cs` counts accepted-use signals for assimilation | `repo://src/CanDoItAll.Modules.CognitiveMemory/Operations/CognitiveMemoryScheduledAutomationRunner.cs` calls `ScanAssimilationAsync` after successful consolidation cycles | `AcceptedUseEmitter_PublishesRecallTraceSignalAndRejectsDirectCaptureMemory` rejects direct capture memory; SB02 red baseline proves producer absence |

## Source Assertions

`bundle://proof/SB03/transcripts/source-assertions.txt` records the production emitter contract, lineage validation against synthesis statement source maps, ledger publication using `SourceKind = CognitiveMemorySignalSourceKind.RecallTrace`, DI registration, and scheduled assimilation scan wiring.

## Red-Team Negative Proof

`bundle://proof/SB03/transcripts/failing-first.txt` records the SB02 red baseline where no production emitter or scheduled scan existed. The new behavior test proves direct professor capture memory is rejected before accepted-use publication.

## Browser And Host Proof

Browser validation: N/A. SB03 changes are backend services, contracts, DI, and unit tests only.

## Downstream Dependency Check

SB10 can use the production emitter instead of seeding accepted-use signals manually.
