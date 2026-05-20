# SB10 Service Size And Responsibility Review

## Size Snapshot

Line counts are recorded in `bundle://proof/SB10/transcripts/service-size-counts.txt`.

- `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryCuratorConversationService.cs`: 1225 lines.
- `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryClusterPlanner.cs`: 1048 lines.
- `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamConsolidationService.cs`: 779 lines.
- `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamModeClusterSelection.cs`: 108 lines.
- `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryClusterFormation.cs`: 667 lines.
- `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryProfessorAssimilationEvaluator.cs`: 255 lines.
- `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryRecallBriefComposition.cs`: 598 lines.

## Refactor Completed

- Extracted dream mode selection and planning-scope policy into `ICognitiveMemoryDreamModeClusterSelector` / `CognitiveMemoryDreamModeClusterSelector`.
- `CognitiveMemoryDreamConsolidationService` now delegates cluster selection, selection reason codes, and planning-scope resolution to that collaborator.
- Added direct collaborator coverage in `SemanticInvariant_DreamModeClusterSelectorKeepsModePolicyOutsideRunOrchestration`.
- Rewired runtime algorithm options through `CognitiveMemoryQualityAlgorithmOptions` DI registration and constructor injection, removing production runtime reads of `CognitiveMemoryQualityAlgorithmOptions.Current`.

## Accepted Residual Risk

- `CognitiveMemoryCuratorConversationService`, `CognitiveMemoryClusterPlanner`, and `CognitiveMemoryDreamConsolidationService` remain large orchestration files. This is an accepted residual risk for this closure because the bundle explicitly warns against risky big-bang rewrites; the extracted selector and option-injection work remove concrete policy/config coupling without changing persisted behavior.
- Future work should split curator turn capture/persistence from response runtime orchestration, and split cluster scoring/readiness from persistence once more characterization tests exist.

## Validation

- Failing-first: `bundle://proof/SB10/transcripts/failing-first-current.txt`.
- Targeted passing tests: `bundle://proof/SB10/transcripts/passing-semantic-tests.txt`.
- Broad cognitive-memory tests: `bundle://proof/SB10/transcripts/broad-cognitive-memory-tests.txt`.
- Static-options guard: `bundle://proof/SB10/transcripts/static-options-guard.txt`.
