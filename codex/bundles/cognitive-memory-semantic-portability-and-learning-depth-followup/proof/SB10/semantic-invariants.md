# SB10 Semantic Invariants

## Invariant SB10-OPTIONS-DI-01

- Invariant ID: `SB10-OPTIONS-DI-01`
- Source raw note: Algorithm options must be injected through DI/config instead of static `Current` access in runtime services.
- Expected behavior: Cognitive-memory runtime services consume a registered `CognitiveMemoryQualityAlgorithmOptions` instance or explicit test override, and production source outside the options definition does not read `CognitiveMemoryQualityAlgorithmOptions.Current`.
- Disallowed shallow implementation: Registering `Current` in DI while services still read static defaults, or only changing tests to inspect the options object without proving service behavior consumes it.
- Failing-first test: `SemanticInvariant_ClusterPlannerConsumesInjectedAlgorithmOptionsForReadiness` in `bundle://proof/SB10/transcripts/failing-first-current.txt` failed before the planner accepted injected algorithm options.
- Passing test: `SemanticInvariant_ClusterPlannerConsumesInjectedAlgorithmOptionsForReadiness` in `bundle://proof/SB10/transcripts/passing-semantic-tests.txt`, plus broad regressions in `bundle://proof/SB10/transcripts/broad-cognitive-memory-tests.txt`.
- Changed source files: `repo://src/CanDoItAll.Modules.CognitiveMemory/CognitiveMemoryModuleServiceCollectionExtensions.cs`, `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryClusterPlanner.cs`, `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryClusterFormation.cs`, `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamConsolidationService.cs`, `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryAggregateMemoryApplicator.cs`, `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryProfessorAssimilationEvaluator.cs`, `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryRecallBriefComposition.cs`, and tests.
- Production assertions: `bundle://proof/SB10/transcripts/source-assertions.txt` and `bundle://proof/SB10/transcripts/static-options-guard.txt` prove DI registration, constructor-injected options, and no production runtime `Current` reads.
- Red-team negative case: A custom low aggregate-ready cluster record limit must demote an otherwise aggregate-ready cluster through the injected planner options.
- Downstream dependency check: Final broad cognitive-memory tests must remain green after the options wiring change.

## Invariant SB10-COLLABORATOR-BOUNDARY-02

- Invariant ID: `SB10-COLLABORATOR-BOUNDARY-02`
- Source raw note: Remaining oversized services must move domain policy out of orchestration into direct collaborators without behavior regression.
- Expected behavior: Dream consolidation mode selection policy is represented by a direct collaborator with unit tests, while `CognitiveMemoryDreamConsolidationService` orchestrates DB/run lifecycle and delegates mode-selection decisions.
- Disallowed shallow implementation: Moving private methods without an interface/testable boundary, or adding an unused collaborator while the service still owns the mode-selection rules.
- Failing-first test: `SemanticInvariant_DreamModeClusterSelectorKeepsModePolicyOutsideRunOrchestration` in `bundle://proof/SB10/transcripts/failing-first-current.txt` failed before the collaborator existed.
- Passing test: `SemanticInvariant_DreamModeClusterSelectorKeepsModePolicyOutsideRunOrchestration` in `bundle://proof/SB10/transcripts/passing-semantic-tests.txt`, plus dream regressions in `bundle://proof/SB10/transcripts/regression-tests.txt`.
- Changed source files: `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamModeClusterSelection.cs`, `repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamConsolidationService.cs`, `repo://src/CanDoItAll.Modules.CognitiveMemory/CognitiveMemoryModuleServiceCollectionExtensions.cs`, and `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryQualityCollaboratorTests.cs`.
- Production assertions: `bundle://proof/SB10/transcripts/source-assertions.txt` proves the selector interface/class, DI registration, and service delegation calls.
- Red-team negative case: Cross-project weekly dreams must select only aggregate-ready clusters with multiple source projects; project nightly may still include needs-review/restricted/contradictory clusters for review workflows.
- Downstream dependency check: Dream consolidation regression tests must remain green after the collaborator extraction.

## Invariant SB10-FINAL-CLOSURE-03

- Invariant ID: `SB10-FINAL-CLOSURE-03`
- Source raw note: Final closure must prove the whole cognitive-memory loop and preserve the explicit economic-governance exclusion.
- Expected behavior: The final proof includes completed-stage bundle validation, fake-proof rejection, targeted tests, broad cognitive-memory tests, anti-stub audit, economic-governance scope guard, service-size/responsibility review, and a red-team verdict covering wrong memory, professor correction, anchor, dream comparison, independent support, accepted use, assimilation/fade, recall brief, and reference resolution.
- Disallowed shallow implementation: Closing the bundle with report prose only, omitting completed-stage validation, or introducing pricing/market/economic-governance code outside this bundle's scope.
- Failing-first test: The prepared bundle state is incomplete until completed-stage proof and final red-team artifacts exist.
- Passing test: `bundle://proof/SB10/transcripts/completed-validator.txt`, `bundle://proof/SB10/transcripts/fake-proof-fixtures.txt`, `bundle://proof/SB10/transcripts/broad-cognitive-memory-tests.txt`, and `bundle://proof/SB10/red-team-verdict.md`.
- Changed source files: `repo://codex/bundles/cognitive-memory-semantic-portability-and-learning-depth-followup/README.md`, `repo://codex/bundles/cognitive-memory-semantic-portability-and-learning-depth-followup/reviews/01-execution-report.md`, `bundle://proof/SB10/manifest.md`, and `bundle://proof/SB10/red-team-verdict.md`.
- Production assertions: `bundle://proof/SB10/transcripts/economic-governance-scope-guard.txt`, `bundle://proof/SB10/transcripts/anti-stub-audit.txt`, and `bundle://proof/SB10/service-size-responsibility-report.md`.
- Red-team negative case: No source file changed by this bundle may add economic governance, pricing, market, budget market, or resource-economics behavior.
- Downstream dependency check: Full final validation covers SB01-SB09 invariants through completed-stage bundle validation plus targeted and broad cognitive-memory regression tests.
