# SB08 Semantic Invariants

## Invariant SB08-RECALL-LINEAGE-01

- Invariant ID: SB08-RECALL-LINEAGE-01
- Source raw note: R08 recall brief line-level reference lineage.
- Expected behavior: Recall brief synthesis keeps source references attached only to the statement support that used them, while preserving explicit aggregate-claim line evidence.
- Disallowed shallow implementation: Shared source items can leak unrelated support into the answer line, making reference-on-demand too broad.
- Failing-first test: bundle://proof/SB08/transcripts/failing-first.txt.
- Passing test: bundle://proof/SB08/transcripts/passing.txt.
- Changed source files: `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryRecallBriefComposition.cs` hash `0588FFEDA4292BD26B0443AAE32C7C090AC49460ABFDCD5D74D886582646C62D`; `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryQualityFoundationTests.cs` hash `27856051661381AA5D341D51D3D8E4C7C1D2C810AE0C1829B2B3B6EF0BC16954`.
- Production assertions: bundle://proof/SB08/transcripts/source-assertions.txt cites the production paths that implement the invariant.
- Red-team negative case: bundle://proof/SB08/transcripts/failing-first.txt records the failing shallow case for the invariant.
- Downstream dependency check: SB08 proof is included in bundle://reviews/01-execution-report.md, the cognitive-memory focused tests, and the completed-stage bundle validation.


