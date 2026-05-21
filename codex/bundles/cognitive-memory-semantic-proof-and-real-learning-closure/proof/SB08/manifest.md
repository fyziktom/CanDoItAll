# SB08 Proof Manifest

## Subbundle

- Subbundle: 08-recall-brief-synthesis-and-reference-lineage
- Status: Completed
- Owned requirements: R08 recall brief line-level reference lineage.
- Test name: `SemanticInvariant_RecallBriefKeepsSharedSourceLineageOnlyForTheStatementSupport`
- Test name: `SemanticInvariant_RecallBriefKeepsAggregateClaimLineageAtStatementLineLevel`

## Changed Files And Hashes

| File | SHA-256 |
|---|---:|
| `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryRecallBriefComposition.cs` | `0588FFEDA4292BD26B0443AAE32C7C090AC49460ABFDCD5D74D886582646C62D` |
| `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryQualityFoundationTests.cs` | `27856051661381AA5D341D51D3D8E4C7C1D2C810AE0C1829B2B3B6EF0BC16954` |

## Proof Artifacts

- Semantic invariant contract: bundle://proof/SB08/semantic-invariants.md
- Failing-first transcript: bundle://proof/SB08/transcripts/failing-first.txt
- Passing transcript: bundle://proof/SB08/transcripts/passing.txt
- Source assertion transcript: bundle://proof/SB08/transcripts/source-assertions.txt
- Anti-stub audit transcript: bundle://proof/SB08/transcripts/anti-stub.txt
- Source assertion: bundle://proof/SB08/transcripts/source-assertions.txt

## Proof Claim To Code Matrix

| Capability claim | Required production source proof | Required test proof | Required negative fixture | Result |
|---|---|---|---|---|
| line-level | repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryRecallBriefComposition.cs and repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryRecallSynthesisService.cs and repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryReferenceResolver.cs | bundle://proof/SB08/transcripts/passing.txt | failing negative rejected by bundle://proof/SB08/transcripts/failing-first.txt | Verified pass |
## Closure

- Failing-first: bundle://proof/SB08/transcripts/failing-first.txt.
- Semantic positive proof: bundle://proof/SB08/transcripts/passing.txt.
- Source assertions: bundle://proof/SB08/transcripts/source-assertions.txt.
- Anti-stub audit: bundle://proof/SB08/transcripts/anti-stub.txt states no stubs were found.



