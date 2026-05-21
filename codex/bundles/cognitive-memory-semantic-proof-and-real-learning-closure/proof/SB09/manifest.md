# SB09 Proof Manifest

## Subbundle

- Subbundle: 09-maintainability-refactor-and-options-boundaries
- Status: Completed
- Owned requirements: R09 maintainability boundaries and injected options.
- Test name: `SemanticInvariant_QualityArchitectureUsesFocusedBoundariesAndInjectedOptions`

## Changed Files And Hashes

| File | SHA-256 |
|---|---:|
| `repo://src/CanDoItAll.Modules.CognitiveMemory/CognitiveMemoryModuleServiceCollectionExtensions.cs` | `2A6D38387C12BF5F3D606E229F8F46136DE862CC28E48546FAE92C01417288C3` |
| `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryClusterFormation.cs` | `0010D63663A3A47DF9837D21C54964BE0071501EC05E1FD9AB051D85735472BB` |
| `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryClusterPlanner.cs` | `717255C67971B4D1503E34D7A523A17567DBF3A55A5C190C0C3CA5F48CD488C6` |
| `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamConsolidationService.cs` | `E185545B52F1920632A85CA52F4D4BD38A44EB216DEFFF890E042837BB0FADB5` |
| `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryRecallBriefComposition.cs` | `0588FFEDA4292BD26B0443AAE32C7C090AC49460ABFDCD5D74D886582646C62D` |
| `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryProfessorAnchorExtraction.cs` | `6565034DE52E3FB8D6DDC41D1B76428417C4F6B3385B61AACD284D842F0FCE46` |
| `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryRecallOutcomeAcceptedEventHandler.cs` | `F27C1A651F2077718FF4F4F8AE3EE3B7AD949C3018299C90C1DA8694C5B830AC` |
| `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryQualityFoundationTests.cs` | `27856051661381AA5D341D51D3D8E4C7C1D2C810AE0C1829B2B3B6EF0BC16954` |

## Proof Artifacts

- Semantic invariant contract: bundle://proof/SB09/semantic-invariants.md
- Failing-first transcript: bundle://proof/SB09/transcripts/failing-first.txt
- Passing transcript: bundle://proof/SB09/transcripts/passing.txt
- Source assertion transcript: bundle://proof/SB09/transcripts/source-assertions.txt
- Anti-stub audit transcript: bundle://proof/SB09/transcripts/anti-stub.txt
- Source assertion: bundle://proof/SB09/transcripts/source-assertions.txt

## Proof Claim To Code Matrix

| Capability claim | Required production source proof | Required test proof | Required negative fixture | Result |
|---|---|---|---|---|
| no semantic capability label asserted | bundle://proof/SB09/transcripts/source-assertions.txt | bundle://proof/SB09/transcripts/passing.txt | failing negative: bundle://proof/SB09/transcripts/failing-first.txt | Verified pass |
## Closure

- Failing-first: bundle://proof/SB09/transcripts/failing-first.txt.
- Semantic positive proof: bundle://proof/SB09/transcripts/passing.txt.
- Source assertions: bundle://proof/SB09/transcripts/source-assertions.txt.
- Anti-stub audit: bundle://proof/SB09/transcripts/anti-stub.txt states no stubs were found.



