# Source Artifacts

| Artifact | Path | Role in this bundle |
|---|---|---|
| Cognitive Memory module source | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory` | Primary implementation under review. |
| Current implementation map | `C:\repositories\CanDoItAll\docs\cognitive-memory\current-state\implementation-map.md` | Current docs after P0/P1 refactors. |
| Runtime flows doc | `C:\repositories\CanDoItAll\docs\cognitive-memory\architecture\runtime-flows.md` | Current documented recall/consolidation flows. |
| Roadmap | `C:\repositories\CanDoItAll\docs\cognitive-memory\roadmap\roadmap.md` | P0/P1 status and beta/alpha boundaries. |
| Multi-cycle validation report | `C:\repositories\CanDoItAll\cognitive-memory-multi-cycle-demo-validation\validation\evidence\20260517-181521\99-run-summary.json` | Older validation evidence; useful but not proof of dream quality. |
| Ingestion/settings validation report | `C:\repositories\CanDoItAll\cognitive-memory-testing-ingestion-settings\validation\evidence\20260517-115640\99-summary.json` | Evidence for ingestion, review previews, source filtering, and deduped context pack changes. |
| Qdrant beta validation report | `C:\repositories\CanDoItAll\docs\cognitive-memory\operations\validation-and-testing.md` | Evidence for projection rebuild/Qdrant vector path. |
| Unit tests - consolidation | `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\CognitiveMemoryConsolidationEngineTests.cs` | Current consolidation coverage; mostly item-level. |
| Unit tests - recall | `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\CognitiveMemoryRecallOrchestratorTests.cs` | Current recall coverage; scoring, lexical/vector fallback, redaction, budget, dedupe. |
| Integration tests - persistence | `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\CognitiveMemoryPersistenceModelTests.cs` | Current persistence model coverage. |
| Playwright review tests | `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\CognitiveMemoryReviewUiPlaywrightTests.cs` | UI proof entry point for review surfaces. |

## Interpretation Rules

- Treat docs as a current intent map, but verify behavior against source code and tests.
- Treat old validation bundles as evidence of core mechanics only; do not assume they prove clustering, dreaming, aggregate validation, or synthesis.
- Preserve existing P0/P1 improvements unless a subbundle explicitly replaces them with a safer equivalent.
