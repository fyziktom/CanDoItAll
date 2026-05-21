# SB07 Semantic Invariants

## Invariant SB07-DOMAIN-PROVENANCE-01

- Invariant ID: SB07-DOMAIN-PROVENANCE-01
- Source raw note: R07 domain dream synthesis and claim-specific provenance.
- Expected behavior: Dream synthesis emits internalized domain statements without diagnostic boilerplate and maps each aggregate claim only to its supporting claim evidence links.
- Disallowed shallow implementation: A candidate can look source-backed while every claim inherits every source span from the source memory record.
- Failing-first test: bundle://proof/SB07/transcripts/failing-first.txt.
- Passing test: bundle://proof/SB07/transcripts/passing.txt.
- Changed source files: `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamConsolidationService.cs` hash `E185545B52F1920632A85CA52F4D4BD38A44EB216DEFFF890E042837BB0FADB5`; `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamSynthesis.cs` hash `E981C99F5C0DE85224368C912B9CC66D1DCF418059343C92EAA802C9ACDAC8D1`; `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryQualitySupport.cs` hash `918183ECFCF3A0C6DCA8BE41730D758DB9178D1A3669CDECAE92D7D5DB7E3BAB`; `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryQualityFoundationTests.cs` hash `27856051661381AA5D341D51D3D8E4C7C1D2C810AE0C1829B2B3B6EF0BC16954`.
- Production assertions: bundle://proof/SB07/transcripts/source-assertions.txt cites the production paths that implement the invariant.
- Red-team negative case: bundle://proof/SB07/transcripts/failing-first.txt records the failing shallow case for the invariant.
- Downstream dependency check: SB07 proof is included in bundle://reviews/01-execution-report.md, the cognitive-memory focused tests, and the completed-stage bundle validation.


