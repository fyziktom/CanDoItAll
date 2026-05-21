# SB03 Semantic Invariants

## Invariant SB03-RED-CORPUS-01

- Invariant ID: SB03-RED-CORPUS-01
- Source raw note: R03 failing-first semantic regression corpus.
- Expected behavior: The regression corpus proves the previously shallow Czech capture, accepted-use, embedding, dream, provenance, and recall-lineage gaps fail before implementation and pass after production changes.
- Disallowed shallow implementation: A broad green test run without explicit negative semantic cases could miss English-only capture, lexical-only clustering, diagnostic dream text, and broad source lineage.
- Failing-first test: bundle://proof/SB03/transcripts/failing-first.txt.
- Passing test: bundle://proof/SB03/transcripts/passing.txt.
- Changed source files: `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryAdvancedServicesTests.cs` hash `7CD57001F5EF077151B02AA81909E861FA43EBE3E9D008C883707D9D90A33F2E`; `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryQualityFoundationTests.cs` hash `27856051661381AA5D341D51D3D8E4C7C1D2C810AE0C1829B2B3B6EF0BC16954`.
- Production assertions: bundle://proof/SB03/transcripts/source-assertions.txt cites the production paths that implement the invariant.
- Red-team negative case: bundle://proof/SB03/transcripts/failing-first.txt records the failing shallow case for the invariant.
- Downstream dependency check: SB03 proof is included in bundle://reviews/01-execution-report.md, the cognitive-memory focused tests, and the completed-stage bundle validation.


