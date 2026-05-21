# SB08 Proof Manifest

## Status

- Subbundle: `SB08 - Task-facing recall brief with real query and lineage`
- Status: `Completed`
- Owned requirements: `R08`
- Raw notes: Recall synthesis must receive real query/intent and preserve statement lineage.
- Semantic invariant contract: `bundle://proof/SB08/semantic-invariants.md`

## Changed File Hashes

| File | SHA-256 |
|---|---:|
| `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryQualityContracts.cs` | `508078B1B99B83E44BFFFD13D266C472830921919CFC850B59AAE39B7B8F10A7` |
| `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryRecallSynthesisService.cs` | `7F5826DD97FEDEAB29C0028943B095B17471D50F893C96FCA498317163692FD3` |
| `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryQualityFoundationTests.cs` | `82BB9D0116260EB97EE7AB9DB6ABC501E99D792654970C20672B69AA70D6D027` |

Full hash transcript: `bundle://proof/SB08/transcripts/changed-file-hashes.txt`.

## Command Transcripts

- Failing-first transcript: `bundle://proof/SB08/transcripts/failing-first.txt`
- Passing transcript: `bundle://proof/SB08/transcripts/passing.txt`
- Source assertions transcript: `bundle://proof/SB08/transcripts/source-assertions.txt`
- Anti-stub audit transcript: `bundle://proof/SB08/transcripts/anti-stub.txt`
- Changed-file hashes: `bundle://proof/SB08/transcripts/changed-file-hashes.txt`

## Tests And Invariants

- Test name: `SemanticInvariant_RecallSynthesisRequestCarriesRealQueryIntentAndLineage`
- Existing recall synthesis/reference tests in `CognitiveMemoryQualityFoundationTests`
- Invariant ID: `SB08-REAL-QUERY-INTENT-01`
- Invariant ID: `SB08-AGGREGATE-LINEAGE-02`

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
|---|---|---|---|---|
| `CognitiveMemoryRecallSynthesisRequest.QueryText` and `Intent` | `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryQualityContracts.cs` defines contract fields | `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryRecallSynthesisService.cs` passes query and intent to composer | Recall synthesis persists synthesized statements/source maps for reference resolution | SB02 red baseline proves old request lacked real query/intent |
| Aggregate claim lineage | `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryRecallSynthesisService.cs` loads aggregate claim ids from selected sections | Synthesized statement source maps persist aggregate claim ids and source refs | `CognitiveMemoryReferenceResolver` resolves statement references on demand | Source assertion rejects old title/summary-only query construction |

## Source Assertions

`bundle://proof/SB08/transcripts/source-assertions.txt` records request contract fields, `request.QueryText` usage, intent propagation, fallback helper, and aggregate claim lineage persistence.

## Red-Team Negative Proof

SB02 baseline showed recall synthesis used context title/summary instead of task query. Passing source assertions reject that old construction.

## Browser And Host Proof

Browser validation: N/A. SB08 is backend recall synthesis logic.

## Downstream Dependency Check

Final recall proof can now use task-facing query terms and exact reference lineage.
