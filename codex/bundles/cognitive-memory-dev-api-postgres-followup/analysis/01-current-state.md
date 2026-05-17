# Current State

## Previous Bundle Status

`cognitive-memory-architecture-v2` is not closed. Its root README says execution is in progress and the execution report shows completion only through the human review UI slice.

Completed or materially implemented slices:

- Module foundation and EF guardrails.
- Score geometry driver.
- Neuro foundation: claim/evidence/context/mutation ledger.
- Source ingestion from workbench/process/workflow source snapshots.
- Semantic/RAG adapter contracts and adapter registration.
- Memory taxonomy and projections.
- Cognitive workspace and attention router.
- Prediction-error and salience signals.
- Recall orchestrator.
- Consolidation engine.
- Temporal replay scheduler.
- Procedural skill memory simulation.
- Human review UI.

Explicitly remaining or not closed in the previous bundle:

- MAF workflow integration.
- Probing regression calibration.
- Cognitive self model.
- Calibration health and probing training.
- Self-regulation orchestrator and UI.
- Professor review escalation.
- Metamemory abstention calibration.
- Interactive memory probing workbench.
- Epistemic drive engine.
- Cross-project memory.
- Distributed idle compute.
- Architecture integration closure.
- Final validation and architecture closure.

## Implementation State From Last Commit

The last commit `1a236d5ccc4b71911903a91c2cfd8013d3705ef8` added a large Cognitive Memory module and migrations. The core storage model, ingestion, consolidation, recall, review UI, replay, procedural memory, score geometry, and signal records exist and compile.

The module registration intentionally throws when required semantic/RAG/embedding services are missing:

- `IAgentTextEmbeddingGenerator`
- `ISemanticTextRanker`
- `IRagDriver`

That behavior is correct. Developer tooling must surface these provider gaps explicitly.

## Done

- Cognitive-memory EF model and migrations exist for SQLite and PostgreSQL.
- Workbench project structure can be exposed as a source snapshot.
- Ingestion API contracts can ingest `WorkbenchProjectStructure`, `ProcessRuntime`, and `WorkflowRuntime` source kinds.
- Consolidation can process source items into candidate/mutation/review records.
- Review UI snapshot and decision APIs exist at service level.
- Recall service and trace records exist, with provider dependencies for semantic/vector work.

## Not Done

- No developer HTTP API existed before this follow-up.
- No Codex skill existed for memory control.
- No PostgreSQL-first smoke bundle existed with realistic sample data.
- No MAF contributor wires Cognitive Memory into agent context.
- No completed probing/self-regulation/metamemory/professor-review subsystems exist.
- No final previous-bundle closure evidence exists.

## Maintainability Risks

Several files are too large for long-term maintenance:

- `CognitiveMemoryRecallServices.cs`: about 1780 lines.
- `CognitiveMemoryConsolidationServices.cs`: about 889 lines.
- `CognitiveMemoryProcedureSkillService.cs`: about 871 lines.
- `CognitiveMemoryTemporalReplayServices.cs`: about 854 lines.
- `CognitiveMemorySourceIngestionService.cs`: about 761 lines.
- `CognitiveMemoryWorkspaceServices.cs`: about 745 lines.
- `CognitiveMemorySignalServices.cs`: about 714 lines.
- `CognitiveMemoryReviewUiService.cs`: about 649 lines.
- `CognitiveMemoryPage.razor` and code-behind: both large UI/orchestration files.

Recommended refactors should be narrow and behavior-preserving:

- Split recall into intent/scope analysis, candidate activation, expansion, focus selection, detail retrieval, and context-pack rendering services.
- Split consolidation into source cursor scanning, candidate extraction, mutation submission, review-item creation, and projection invalidation components.
- Move review UI queries into focused read models/query handlers.
- Extract provider-health checks so unavailable semantic/RAG capabilities can be reported without resolving the full orchestrator.
