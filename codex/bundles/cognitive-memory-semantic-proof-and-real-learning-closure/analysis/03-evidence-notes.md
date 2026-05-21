# Evidence Notes

## Completed validator portability check

Command executed from the extracted review checkout:

```text
python codex/skills/bundles/candoitall-bundle-preparation/scripts/validate_bundle.py codex/bundles/cognitive-memory-production-signal-and-deep-synthesis-followup --profile initiative --stage completed --repo-root /mnt/data/review_cogmem4/CanDoItAll-cognitive-memory
```

Observed result:

```text
Bundle validation failed:
- .../proof/SB01/manifest.md: referenced artifact path does not exist: C:\Users\lucys\.codex\skills\candoitall-bundle-preparation\scripts\validate_bundle.py
```

## Source contradiction examples

- `repo://codex/bundles/cognitive-memory-production-signal-and-deep-synthesis-followup/reviews/01-execution-report.md` says SB05 proved Czech/diacritic capture.
- `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryProfessorAnchorExtraction.cs` still contains English-only signal arrays and no diacritic folding for matching.
- `repo://codex/bundles/cognitive-memory-production-signal-and-deep-synthesis-followup/reviews/01-execution-report.md` says SB07 proved embedding-backed approximate clustering.
- `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryClusterFormation.cs` names a provider `EmbeddingBacked` but uses lexical signal extraction and `ICognitiveMemoryClusterSemanticSimilarityProvider`, not `ICognitiveMemoryEmbeddingProvider`.
- `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamSynthesis.cs` still emits meta text about mapped source claims.
- `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryQualitySupport.cs` does not load `CognitiveMemoryClaimEvidenceLinkRecord`, so dream claim provenance cannot be truly claim-specific.

## Maintainability notes

The current implementation should not receive more broad feature changes without splitting the largest services into testable responsibilities. Otherwise Codex tends to satisfy one test at a time inside large orchestration methods instead of producing durable architecture.
