# SB05 Proof Manifest

## Status

- Subbundle: `SB05 - Natural multilingual professor capture`
- Status: `Completed`
- Owned requirements: `R05`
- Raw notes: Czech Q&A teaching and diacritic-bearing correction signals must create structured professor anchors without stripping stored text.
- Semantic invariant contract: `bundle://proof/SB05/semantic-invariants.md`

## Changed File Hashes

| File | SHA-256 |
|---|---:|
| `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryProfessorAnchorExtraction.cs` | `8D01EEE267859D16F61C7F8EBCE18B425574AA25E647F2E51AB39A8043C43B38` |
| `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryAdvancedServicesTests.cs` | `AA63276727CD6793DF76447A03F42E445431AB57774EF65D050B5D89231AE2CB` |

Full hash transcript: `bundle://proof/SB05/transcripts/changed-file-hashes.txt`.

## Command Transcripts

- Failing-first transcript: `bundle://proof/SB05/transcripts/failing-first.txt`
- Passing transcript: `bundle://proof/SB05/transcripts/passing.txt`
- Source assertions transcript: `bundle://proof/SB05/transcripts/source-assertions.txt`
- Anti-stub audit transcript: `bundle://proof/SB05/transcripts/anti-stub.txt`
- Changed-file hashes: `bundle://proof/SB05/transcripts/changed-file-hashes.txt`

## Tests And Invariants

- Test name: `SemanticInvariant_CuratorCaptureCzechDiacriticsAndNaturalScopeCreatesProfessorAnchor`
- Invariant ID: `SB05-DIACRITIC-INSENSITIVE-CAPTURE-01`
- Invariant ID: `SB05-PRESERVE-STORED-TEXT-02`

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
|---|---|---|---|---|
| Professor anchor capture from Czech Q&A | `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryProfessorAnchorExtraction.cs` extracts claims and scope from normalized search text | `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryCuratorConversationService.cs` persists the returned anchor as `Active` | Curator capture creates source item, evidence anchor, mutation candidate, and capture record through the existing trusted-conversation path | SB02 red baseline proves the natural Czech Q&A flow previously produced no capture |

## Source Assertions

`bundle://proof/SB05/transcripts/source-assertions.txt` records diacritic-insensitive search normalization, Czech lead-ins/signals, original source utterance preservation, and active-anchor persistence through the curator service.

## Red-Team Negative Proof

The extractor still requires teaching intent, useful claims, and target scope. Casual question-only input without teaching context remains ignored.

## Browser And Host Proof

Browser validation: N/A. SB05 is backend curator extraction logic and unit coverage.

## Downstream Dependency Check

SB10 can use natural Czech professor teaching in the final lifecycle proof.
