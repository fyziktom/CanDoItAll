# SB07 Proof Manifest

## Subbundle

- Subbundle: 07-domain-dream-synthesis-and-claim-provenance
- Status: Completed
- Owned requirements: R07 domain dream synthesis and claim-specific provenance.
- Test name: `DreamRun_CanonicalAggregateMemoryContainsDomainKnowledgeWithoutDiagnosticBoilerplate`
- Test name: `SemanticInvariant_DreamRunUsesClaimEvidenceLinksInsteadOfRecordWideSourceMaps`

## Changed Files And Hashes

| File | SHA-256 |
|---|---:|
| `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamConsolidationService.cs` | `E185545B52F1920632A85CA52F4D4BD38A44EB216DEFFF890E042837BB0FADB5` |
| `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamSynthesis.cs` | `E981C99F5C0DE85224368C912B9CC66D1DCF418059343C92EAA802C9ACDAC8D1` |
| `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryQualitySupport.cs` | `918183ECFCF3A0C6DCA8BE41730D758DB9178D1A3669CDECAE92D7D5DB7E3BAB` |
| `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryQualityFoundationTests.cs` | `27856051661381AA5D341D51D3D8E4C7C1D2C810AE0C1829B2B3B6EF0BC16954` |

## Proof Artifacts

- Semantic invariant contract: bundle://proof/SB07/semantic-invariants.md
- Failing-first transcript: bundle://proof/SB07/transcripts/failing-first.txt
- Passing transcript: bundle://proof/SB07/transcripts/passing.txt
- Source assertion transcript: bundle://proof/SB07/transcripts/source-assertions.txt
- Anti-stub audit transcript: bundle://proof/SB07/transcripts/anti-stub.txt
- Source assertion: bundle://proof/SB07/transcripts/source-assertions.txt

## Proof Claim To Code Matrix

| Capability claim | Required production source proof | Required test proof | Required negative fixture | Result |
|---|---|---|---|---|
| claim-specific | repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryQualitySupport.cs and repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamConsolidationService.cs | bundle://proof/SB07/transcripts/passing.txt | failing negative rejected by bundle://proof/SB07/transcripts/failing-first.txt | Verified pass |
| domain synthesis | repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamSynthesis.cs and repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamConsolidationService.cs and repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryQualityContracts.cs | bundle://proof/SB07/transcripts/passing.txt | failing negative rejected by bundle://proof/SB07/transcripts/failing-first.txt | Verified pass |
## Closure

- Failing-first: bundle://proof/SB07/transcripts/failing-first.txt.
- Semantic positive proof: bundle://proof/SB07/transcripts/passing.txt.
- Source assertions: bundle://proof/SB07/transcripts/source-assertions.txt.
- Anti-stub audit: bundle://proof/SB07/transcripts/anti-stub.txt states no stubs were found.



