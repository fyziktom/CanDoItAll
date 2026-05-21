# Current State Review

## Meaningful improvements found

- `CognitiveMemoryProfessorAcceptedUseSignalEmitter` exists and is registered through DI.
- The emitter validates actor/policy, recall trace, synthesis, statement, derived memory, source maps, and evidence before publishing a `ProfessorAnchorAcceptedUse` signal.
- Scheduled automation can call `ScanAssimilationAsync` after successful consolidation cycles.
- `CognitiveMemoryProfessorReviewService.ResolveComparisonAsync` exists and can resolve comparing professor anchors through typed outcomes.
- Cross-project clustering is no longer blocked by the older project-only pair rejection path.
- Recall synthesis has explicit `QueryText` and `Intent` inputs and stores statement source maps.
- The bundle workflow skill and validator are much stronger than early versions: proof manifests, transcripts, SHA-256 hashes, semantic invariants, and production artifact matrices now exist.

## Critical remaining gaps

### Completed proof is still not portable

Running the completed-stage validator from a moved checkout failed because `proof/SB01/manifest.md` references `C:\Users\lucys\.codex\skills\candoitall-bundle-preparation\scripts\validate_bundle.py`. This violates the intended `repo://` / `bundle://` portability model and proves the previous portability closure is incomplete.

### Report claims can still outrun source behavior

The latest execution report claims that Czech/diacritic professor capture is implemented. The reviewed production extractor still uses English-only signals such as `because`, `example`, `counterexample`, `not`, `instead`, `source of truth`, `approval`, and English question lead-ins. It does not contain diacritic-insensitive matching, Czech signal dictionaries, or Czech question/correction patterns.

The latest execution report also claims embedding-backed approximate clustering. The provider named `CognitiveMemoryEmbeddingBackedApproximateClusterCandidateProvider` does not inject or call `ICognitiveMemoryEmbeddingProvider`. It uses `ICognitiveMemoryClusterSemanticSimilarityProvider`, rare lexical signals, and aliases. This is a lexical approximate provider, not embedding-backed discovery.

### Professor accepted-use lifecycle is not yet fully integrated

The accepted-use emitter is a real production service, but current grep evidence shows it is called primarily from tests and proof artifacts. The bundle must require integration with a real outcome/feedback event path so accepted-use evidence can be emitted when an agent/user actually accepts or uses a synthesized memory-backed answer.

### Dream synthesis is still meta-synthesis

`CognitiveMemoryDreamClaimSynthesizer` returns text like `Claim: X is consistently described across the mapped source claims` and `The source claims stay separated across N subject(s)`. This is not a useful internalized memory. It is a diagnostic summary about source maps.

### Claim-specific provenance is still too broad

`CognitiveMemoryQualitySupportLoader` does not load `CognitiveMemoryClaimEvidenceLinkRecord`. `CreateClaimUnits` attaches all source maps for a memory record to every claim unit from that record. `CreateClaimSpecificSourceMaps` then filters by record and signature, not by the evidence links for the exact claim. This can still attach unrelated source/evidence anchors to an aggregate claim.

### Recall brief is better but still fragment-based

The recall composer selects useful lines from selected context sections, groups them, and prefixes them as `Answer -`, `Action -`, or `Caveat -`. This is safer than raw context dump, but it is not yet a task-facing synthesis engine that rewrites memory into the form needed by the requester while preserving exact statement-level lineage.

### Maintainability risk remains high

Several services are still too large and internally mixed:

- `CognitiveMemoryCuratorConversationService.cs` is around 1200 lines.
- `CognitiveMemoryClusterPlanner.cs` is around 1000 lines.
- `CognitiveMemoryDreamConsolidationService.cs` is around 800 lines.
- `CognitiveMemoryDreamSynthesis.cs` is around 800 lines.
- `CognitiveMemoryRecallBriefComposition.cs` is around 600 lines.

These files mix orchestration, extraction, scoring, persistence, formatting, and validation logic, which makes future Codex passes prone to shallow edits and hidden regressions.

