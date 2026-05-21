# SB04 Proof Manifest

## Subbundle

- Subbundle: 04-real-czech-professor-teaching-extractor
- Status: Completed
- Owned requirements: R04 Czech professor teaching capture with diacritics.
- Test name: `SemanticInvariant_CuratorCaptureCzechProfessorTeachingWithoutEnglishKeywordsPreservesDiacritics`

## Changed Files And Hashes

| File | SHA-256 |
|---|---:|
| `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryProfessorAnchorExtraction.cs` | `6565034DE52E3FB8D6DDC41D1B76428417C4F6B3385B61AACD284D842F0FCE46` |
| `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryCuratorConversationService.cs` | `1257A829C07154C670023628C1F908D399CD1E0F193672A54136F2803B61F6D6` |
| `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryAdvancedServicesTests.cs` | `7CD57001F5EF077151B02AA81909E861FA43EBE3E9D008C883707D9D90A33F2E` |

## Proof Artifacts

- Semantic invariant contract: bundle://proof/SB04/semantic-invariants.md
- Failing-first transcript: bundle://proof/SB04/transcripts/failing-first.txt
- Passing transcript: bundle://proof/SB04/transcripts/passing.txt
- Source assertion transcript: bundle://proof/SB04/transcripts/source-assertions.txt
- Anti-stub audit transcript: bundle://proof/SB04/transcripts/anti-stub.txt
- Source assertion: bundle://proof/SB04/transcripts/source-assertions.txt

## Proof Claim To Code Matrix

| Capability claim | Required production source proof | Required test proof | Required negative fixture | Result |
|---|---|---|---|---|
| Czech/diacritic | repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryProfessorAnchorExtraction.cs | bundle://proof/SB04/transcripts/passing.txt | failing negative rejected by bundle://proof/SB04/transcripts/failing-first.txt | Verified pass |
## Closure

- Failing-first: bundle://proof/SB04/transcripts/failing-first.txt.
- Semantic positive proof: bundle://proof/SB04/transcripts/passing.txt.
- Source assertions: bundle://proof/SB04/transcripts/source-assertions.txt.
- Anti-stub audit: bundle://proof/SB04/transcripts/anti-stub.txt states no stubs were found.



