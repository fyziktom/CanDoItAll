# Scope Inventory

## In Scope Source Areas

| Area | Representative files | Why included |
|---|---|---|
| Consolidation | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Consolidation\CognitiveMemoryConsolidationServices.cs`, `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Consolidation\CognitiveMemoryConsolidationContracts.cs`, `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Consolidation\CognitiveMemoryConsolidationFactExtractor.cs`, `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Consolidation\CognitiveMemoryConsolidationCandidateApplicator.cs` | Current consolidation is the main location of shallow per-item behavior. |
| Foundation records | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Foundation\CognitiveMemoryEntities.cs`, `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Neuro\CognitiveMemoryNeuroFoundationEntities.cs` | Existing memory, claim, source, evidence, relation, and mutation records must be extended safely. |
| Mutation and review | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Neuro\CognitiveMemoryMutationAuthority.cs`, `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\ReviewUi\CognitiveMemoryReviewUiService.cs` | Aggregate candidates must be validated and reviewed before activation. |
| Recall | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Recall\CognitiveMemoryRecallServices.cs`, `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Recall\CognitiveMemoryRecallChannels.cs`, `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Recall\CognitiveMemoryRecallEvaluation.cs`, `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Recall\CognitiveMemoryRecallContextPackBuilder.cs`, `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Recall\CognitiveMemoryRecallMappingAndTypes.cs` | Current retrieval must become synthesis plus reference-on-demand. |
| Agent-facing package | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Advanced\CognitiveMemoryAgentContextPackage.cs`, `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Advanced\CognitiveMemoryAnswerGateService.cs` | Agent context should be concise and gated, not a raw diagnostic dump. |
| Tests | `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\CognitiveMemoryConsolidationEngineTests.cs`, `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\CognitiveMemoryRecallOrchestratorTests.cs`, `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\CognitiveMemoryPersistenceModelTests.cs`, `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\CognitiveMemoryReviewUiPlaywrightTests.cs` | Tests must expand from mechanics to memory quality. |
| Docs and previous bundles | `C:\repositories\CanDoItAll\docs\cognitive-memory\current-state\implementation-map.md`, `C:\repositories\CanDoItAll\docs\cognitive-memory\architecture\runtime-flows.md`, `C:\repositories\CanDoItAll\cognitive-memory-multi-cycle-demo-validation\validation\evidence\20260517-181521\99-run-summary.json` | Documentation and old validation evidence explain current intent and proof gaps. |

## Out Of Scope Source Areas

- Memory economy/governance modules that do not yet belong to the base quality loop.
- Unrelated plugins, storage drivers, process canvas, or generic agent workflow modules unless Codex needs a minimal integration point.
- Broad UI redesign outside Cognitive Memory review/diagnostic surfaces.
