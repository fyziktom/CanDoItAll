# Source Artifacts

| Artifact | Path | Role in this bundle |
|---|---|---|
| Cognitive Memory module source | `/mnt/data/cogmem_repo/CanDoItAll-cognitive-memory/src/CanDoItAll.Modules.CognitiveMemory` | Primary implementation under review. |
| Current implementation map | `/mnt/data/cogmem_repo/CanDoItAll-cognitive-memory/docs/cognitive-memory/current-state/implementation-map.md` | Current docs after P0/P1 refactors. |
| Runtime flows doc | `/mnt/data/cogmem_repo/CanDoItAll-cognitive-memory/docs/cognitive-memory/architecture/runtime-flows.md` | Current documented recall/consolidation flows. |
| Roadmap | `/mnt/data/cogmem_repo/CanDoItAll-cognitive-memory/docs/cognitive-memory/roadmap/roadmap.md` | P0/P1 status and beta/alpha boundaries. |
| Multi-cycle validation report | `/mnt/data/cogmem_repo/CanDoItAll-cognitive-memory/cognitive-memory-multi-cycle-demo-validation/reviews/01-execution-report.md` | Older validation evidence; useful but not proof of dream quality. |
| Ingestion/settings validation report | `/mnt/data/cogmem_repo/CanDoItAll-cognitive-memory/cognitive-memory-testing-ingestion-settings/reviews/01-execution-report.md` | Evidence for ingestion, review previews, source filtering, and deduped context pack changes. |
| Qdrant beta validation report | `/mnt/data/cogmem_repo/CanDoItAll-cognitive-memory/codex/bundles/cognitive-memory-beta-qdrant-validation/reviews/01-execution-report.md` | Evidence for projection rebuild/Qdrant vector path. |
| Unit tests - consolidation | `/mnt/data/cogmem_repo/CanDoItAll-cognitive-memory/tests/CanDoItAll.Tests.Unit/CognitiveMemoryConsolidationEngineTests.cs` | Current consolidation coverage; mostly item-level. |
| Unit tests - recall | `/mnt/data/cogmem_repo/CanDoItAll-cognitive-memory/tests/CanDoItAll.Tests.Unit/CognitiveMemoryRecallOrchestratorTests.cs` | Current recall coverage; scoring, lexical/vector fallback, redaction, budget, dedupe. |
| Integration tests - persistence | `/mnt/data/cogmem_repo/CanDoItAll-cognitive-memory/tests/CanDoItAll.Tests.Integration/CognitiveMemoryPersistenceModelTests.cs` | Current persistence model coverage. |
| Playwright review tests | `/mnt/data/cogmem_repo/CanDoItAll-cognitive-memory/tests/CanDoItAll.Tests.Playwright/CognitiveMemoryReviewUiPlaywrightTests.cs` | UI proof entry point for review surfaces. |

## Interpretation Rules

- Treat docs as a current intent map, but verify behavior against source code and tests.
- Treat old validation bundles as evidence of core mechanics only; do not assume they prove clustering, dreaming, aggregate validation, or synthesis.
- Preserve existing P0/P1 improvements unless a subbundle explicitly replaces them with a safer equivalent.
