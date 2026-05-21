# SB06 Proof Manifest

## Status

- Subbundle: `SB06 - Deep dream synthesis and claim-specific provenance`
- Status: `Completed`
- Owned requirements: `R06`
- Raw notes: Dream output must be domain-useful and provenance must be claim-scoped.
- Semantic invariant contract: `bundle://proof/SB06/semantic-invariants.md`

## Changed File Hashes

| File | SHA-256 |
|---|---:|
| `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamSynthesis.cs` | `BA17DECE431D25DD39770A1068321BA92FC26F132BDAEB3B8BFA77F6640251B3` |
| `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamConsolidationService.cs` | `8603DD580C2B825D5D4449A69F2ADF8881EA5DEEAB89FE083A6ADA3D9A115F0A` |
| `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryQualityFoundationTests.cs` | `82BB9D0116260EB97EE7AB9DB6ABC501E99D792654970C20672B69AA70D6D027` |

Full hash transcript: `bundle://proof/SB06/transcripts/changed-file-hashes.txt`.

## Command Transcripts

- Failing-first transcript: `bundle://proof/SB06/transcripts/failing-first.txt`
- Passing transcript: `bundle://proof/SB06/transcripts/passing.txt`
- Source assertions transcript: `bundle://proof/SB06/transcripts/source-assertions.txt`
- Anti-stub audit transcript: `bundle://proof/SB06/transcripts/anti-stub.txt`
- Changed-file hashes: `bundle://proof/SB06/transcripts/changed-file-hashes.txt`

## Tests And Invariants

- Test name: `DreamRun_CanonicalAggregateMemoryContainsDomainKnowledgeWithoutDiagnosticBoilerplate`
- Test name: `SemanticInvariant_DreamConsolidationCreatesClaimSpecificSourceMaps`
- Test name: `SemanticInvariant_DreamClaimSynthesisProducesStructuredSlots`
- Invariant ID: `SB06-DREAM-TEXT-01`
- Invariant ID: `SB06-CLAIM-SOURCE-MAPS-02`

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
|---|---|---|---|---|
| Dream aggregate claim text | `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamSynthesis.cs` produces `Claim/Evidence/Condition/Caveat` text | `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamConsolidationService.cs` stores synthesized claim text in aggregate candidates | Dream validation/applicator consume aggregate candidates through existing quality lifecycle | SB02 red baseline proves old diagnostic-style output failed |
| Claim-specific source maps | `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamConsolidationService.cs` uses `CreateClaimSpecificSourceMaps` | `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryReferenceResolver.cs` resolves statement/source lineage from persisted source maps | Dream run persists source maps per aggregate claim id | Source assertion rejects the old broad source-map flattening expression |

## Source Assertions

`bundle://proof/SB06/transcripts/source-assertions.txt` records removal of diagnostic conclusion wording, `CreateClaimSpecificSourceMaps`, `ClaimSourceMap` normalization support, and the absence of the broad source-map flattening expression.

## Red-Team Negative Proof

`bundle://proof/SB06/transcripts/failing-first.txt` cites SB02 failures where dream text and source-map scope were too shallow. Green tests now verify production output and source boundaries.

## Browser And Host Proof

Browser validation: N/A. SB06 is backend quality synthesis/provenance logic.

## Downstream Dependency Check

SB08 and SB10 can rely on exact statement-to-claim-to-source lineage.
