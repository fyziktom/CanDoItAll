# Neuroscience To CanDoItAll System Mapping

| Human cognitive function | Biological analogy | CanDoItAll component | Existing source support | New component needed |
|---|---|---|---|---|
| Working memory | Prefrontal working set | Active process/workflow context, active task context | Process runtime, workflow run state, MAF session | `WorkingMemoryScope`, `RecallContextPack` |
| Attention/focus | Executive control | Recall orchestration and process gating | MAF workflows, process definitions | `IRecallOrchestrator`, focus policy |
| Episodic memory | What happened, when, with whom | Process runs, workflow runs, agent executions, artifacts | Process runtime models, workflow store, execution logs | `EpisodicMemoryExtractor` |
| Spatial/context index | Hippocampal place/context indexing | Mindmap/project graph with coordinates | ProjectObjectRecord, ProjectObjectLinkRecord | `MindMapSourceAdapter`, `SpatialFeatureExtractor` |
| Semantic memory | Stable concepts/facts | Project/cross-project canonical topics | RAG repo, semantic driver, search index | `CanonicalMemoryStore`, `SemanticTopicProjector` |
| Procedural memory | Skills/habits | Reusable process templates, workflow templates, runbooks | Processes, workflows, plugins, validation artifacts | `ProcedureExtractor`, `ProcedureMemoryRecord` |
| Salience | Amygdala-like importance/risk weighting | Importance/confidence/risk/rework/failure weighting | Process conformance, artifacts, validation, failures | `MemoryActivationService` |
| Prediction error | Learning from surprise/failure | QA failures, tests, rejected artifacts | Validation module, process improvement candidates | `ContradictionDetector`, `ReflectionAgent` |
| Sleep consolidation | Replay, abstraction, pruning | Idle/night memory processing | Automation/Quartz, background workers | `MemoryConsolidationEngine` |
| Metamemory | Knowing what is known/unknown | Coverage map, uncertainty, review queue | Partial search/indexing only | `MetacognitiveMemoryIndex`, `HumanReviewQueue` |
| Social memory | Who knows/does what | HR/CRM and agent capability mapping | CRM/HR, agent catalog, assignments | memory links to agents/roles/capabilities |
| Cognitive workspace | Active limited working set | Workspace frames, focus slots, inhibited candidates, open questions | Recall traces, process/workflow context | `ICognitiveWorkspaceService` |
| Executive attention | Operation selection | Recall/probe/source-audit/review/learning/replay/abstention routing | MAF workflows and recall orchestration | `IAttentionRouter` |
| Belief revision | Support/attack evidence | Atomic claims, evidence anchors, belief state, mutation authority | Source refs and review queue | `IClaimEvidenceLedger`, `IMemoryMutationAuthority` |
| Context binding | Scope separation | Entity registry, aliases, context frames, context-boundary rules | Project graph, tags, scopes | `IEntityContextBindingService` |
| Prediction error ledger | Expected-vs-observed mismatch | Prediction expectation/error records | QA failures, probe feedback, workflow failures | `IPredictionErrorEngine` |
| Salience signals | Novelty/risk/reward/usefulness | Durable signal vector ledger | Activation/rework/failure signals | `ICognitiveSignalLedger` |
| Replay/rehearsal | Reprocessing important weak memories | Replay scheduler and replay jobs | Quartz/automation/distributed workers | `IReplayScheduler` |
| Procedural skill | Validated action policy | Procedure skills, failure modes, maturity, automation binding | Workflow/process templates | `IProcedureSkillMemoryService` |
| Simulation/metaphor | Hypothetical planning | Speculative simulation sandbox | Learning proposals and process planning | `ISimulationSandboxService` |
| Metamemory gate | Confidence/uncertainty awareness | Answer gate decisions and abstention | Calibration records and warnings | `IMetamemoryAnswerGate` |

## Important Architectural Rule

The system should not imitate biology literally. The mapping is used to design robust software responsibilities:

- hippocampus-like = index/context/association,
- neocortex-like = stable canonical abstractions,
- executive-control-like = workflow/process/recall orchestration,
- sleep-like = scheduled consolidation and replay,
- salience-like = activation scoring and risk-aware ranking.
