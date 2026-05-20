# Scope Inventory

## Backend Services In Scope

| Area | Current file | Review status |
|---|---|---|
| Cluster planning | `/mnt/data/review/CanDoItAll-development/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryClusterPlanner.cs` | Needs P0 redesign from single-key grouping to weighted composite clustering. |
| Dream run orchestration | `/mnt/data/review/CanDoItAll-development/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamConsolidationService.cs` | Needs deeper selection, candidate generation, and mode-specific behavior. |
| Dream validation | `/mnt/data/review/CanDoItAll-development/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamValidator.cs` | Needs quality/independence/semantic gates. |
| Aggregate apply | `/mnt/data/review/CanDoItAll-development/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryAggregateMemoryApplicator.cs` | Needs calibrated confidence, dedupe, lineage, and invalidation hooks. |
| Recall synthesis | `/mnt/data/review/CanDoItAll-development/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryRecallSynthesisService.cs` | Needs true brief synthesis and integration into agent-facing recall. |
| Reference resolution | `/mnt/data/review/CanDoItAll-development/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryReferenceResolver.cs` | Needs provenance expansion through aggregate memories. |
| Curator conversation | `/mnt/data/review/CanDoItAll-development/src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryCuratorConversationService.cs` | Needs target-safe professor learning and assimilation lifecycle. |
| Recall focus evaluation | `/mnt/data/review/CanDoItAll-development/src/CanDoItAll.Modules.CognitiveMemory/Recall/CognitiveMemoryRecallEvaluation.cs` | Prior SideContext bug appears fixed; protect with regression tests. |

## UI Surfaces In Scope

| Area | Current file | Expected change |
|---|---|---|
| Curator tab | `/mnt/data/review/CanDoItAll-development/src/CanDoItAll.Modules.CognitiveMemory/Pages/Components/CognitiveMemoryCuratorTab.razor` | Add capture/target/scope visibility and ambiguity review affordances if backend requires human choice. |
| Curator page logic | `/mnt/data/review/CanDoItAll-development/src/CanDoItAll.Modules.CognitiveMemory/Pages/CognitiveMemoryPage.Curator.cs` | Pass explicit capture kind/targets when user chooses them; expose result warnings. |
| Quality tab | `/mnt/data/review/CanDoItAll-development/src/CanDoItAll.Modules.CognitiveMemory/Pages/CognitiveMemoryPage.Quality.cs` | Surface cluster quality metrics, dream warnings, and validation failures for operator inspection. |

## Tests In Scope

| Test area | Current file | Gap |
|---|---|---|
| Quality foundation | `/mnt/data/review/CanDoItAll-development/tests/CanDoItAll.Tests.Unit/CognitiveMemoryQualityFoundationTests.cs` | Current tests validate plumbing and broad family creation; add quality regression tests. |
| Advanced/curator services | `/mnt/data/review/CanDoItAll-development/tests/CanDoItAll.Tests.Unit/CognitiveMemoryAdvancedServicesTests.cs` | Current tests validate happy paths; add broad-target, multilingual, and assimilation tests. |
| Component tests | `/mnt/data/review/CanDoItAll-development/tests/CanDoItAll.Tests.Components/CognitiveMemoryPageTests.cs` | Add curator UI target/ambiguity proof and quality metrics display proof where changed. |
