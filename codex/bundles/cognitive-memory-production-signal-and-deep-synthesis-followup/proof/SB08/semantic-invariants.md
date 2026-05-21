# SB08 Semantic Invariants

## Invariant SB08-REAL-QUERY-INTENT-01

- Invariant ID: `SB08-REAL-QUERY-INTENT-01`
- Source raw note: Recall synthesis must use the real user query and intent.
- Expected behavior: `CognitiveMemoryRecallSynthesisRequest` carries `QueryText` and `Intent`; synthesis passes them into `CognitiveMemoryRecallBriefComposerRequest`.
- Disallowed shallow implementation: Reconstructing query from context pack title/summary only.
- Failing-first test: `SemanticInvariant_RecallSynthesisRequestCarriesRealQueryIntentAndLineage` failed in SB02 baseline.
- Passing test: `SemanticInvariant_RecallSynthesisRequestCarriesRealQueryIntentAndLineage`.
- Changed source files: `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryQualityContracts.cs` and `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryRecallSynthesisService.cs`.
- Production assertions: `ResolveSynthesisQueryText` uses `request.QueryText` first and only falls back when blank.
- Red-team negative case: Source assertion rejects the old title/summary-only construction.
- Downstream dependency check: Brief composer receives task terms and intent.

## Invariant SB08-AGGREGATE-LINEAGE-02

- Invariant ID: `SB08-AGGREGATE-LINEAGE-02`
- Source raw note: Synthesized statements must preserve exact statement-to-claim-to-source lineage.
- Expected behavior: Aggregate claim ids are loaded from selected recall sections and persisted per synthesized statement source map.
- Disallowed shallow implementation: Broad lineage with no aggregate claim id connection.
- Failing-first test: SB02 baseline required `AggregateClaimIds` lineage preservation.
- Passing test: Existing quality foundation recall/reference tests and the SB08 source invariant.
- Changed source files: `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryRecallSynthesisService.cs`.
- Production assertions: `LoadAggregateClaimIdsAsync` feeds the composer and persistence writes aggregate claim ids into source maps.
- Red-team negative case: Duplicate source maps are deduplicated by memory, claim, source item, and evidence anchor.
- Downstream dependency check: Reference resolver can trace synthesized statements back to aggregate claims.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
|---|---|---|---|---|
| Recall query/intent synthesis request | `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryQualityContracts.cs` | `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryRecallSynthesisService.cs` | `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryRecallBriefComposition.cs` | `bundle://proof/SB08/transcripts/anti-stub.txt` |
