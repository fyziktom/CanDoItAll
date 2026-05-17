# CanDoItAll Cognitive Memory Architecture Bundle

## Profile

- `initiative`

## Mission

Design a biologically inspired, enterprise-grade Cognitive Memory module for CanDoItAll. The module must behave less like a flat RAG index and more like a disciplined memory system: coarse associative recall first, focused attention second, detail retrieval third, and idle-time consolidation afterwards.

The refreshed architecture also adds Epistemic Drive: a metacognitive layer that detects important knowledge gaps, models knowledge need as a multi-dimensional vector, and creates human-reviewable learning proposals instead of behaving like passive retrieval or random curiosity. This update adds Interactive Memory Probing: a dialogue-based interrogation and calibration loop where the user can test memory like a student, inspect source-backed reasoning, correct mistakes, and turn failures into reviewable evidence and regression tests.

The neuro-cognitive patch adds the missing control and belief-management layer: cognitive workspace frames, attention routing, prediction error, salience signals, claim/evidence/belief ledger, entity/context binding, temporal replay, procedural skill memory, simulation safety, and metamemory answer gating. These are now integrated as prerequisites where they affect source ingestion, projection, recall, probing, learning, cross-project promotion, and distributed compute.

The score-geometry update adds the missing reusable scoring foundation. Recall, attention, belief, salience, replay, probing, answer gating, Epistemic Drive, and cross-project promotion must use typed score spaces, vectors, shapes/regions, scalar projection policy, and evaluation traces instead of local add/subtract formulas.

The cognitive self-regulation update connects the distributed control pieces into an explicit self-model, calibration health, humility trigger, answer posture, and professor-review layer. This layer is calibrated agency under epistemic uncertainty, not consciousness or prompt persona. It coordinates when the system should act, caveat, clarify, source-audit, probe, review, escalate, learn, replay, or abstain.

The execution-control update adds a durable phase ledger for implementation agents. The implementation is long enough that markdown alone is not a reliable memory surface; agents must keep the checklist workbook and execution report synchronized after every phase.

This bundle was prepared as architecture and planning. The current execution thread is implementing it under the separate user implementation request and tracking each phase in the workbook and execution report.

## Outcome Contract

- Requested outcome: a validated architecture bundle that another implementation agent can execute phase by phase without rediscovering the memory model, data ownership, integration boundaries, or proof obligations.
- Hard constraints: Qdrant is a rebuildable projection, raw source provenance is mandatory, generated summaries are not source truth, MAF is executive control rather than durable memory storage, and distributed workers cannot mutate authoritative memory.
- Evidence required before closure: source-backed architecture updates, dependency-aware subbundles, explicit progression gates, traceability rows, current-source inspection notes, and a separate prerequisite-refactor bundle if existing code must change before implementation starts.
- Known blockers or explicit scope exceptions: no implementation is included; build/test proof is not required for this architecture repair. The supplied current code indicates that the MAF/source-boundary prerequisite has already been implemented and tested; Codex must still validate the target branch before consuming it.

## Key Decision

Qdrant/RAG is not the memory. It is a rebuildable projection layer. The durable memory is the combination of:

- raw source manifests and immutable source references,
- canonical memory items,
- explicit memory graph relations,
- episodic records from process, workflow, and agent execution,
- procedural records extracted from successful work,
- activation, confidence, staleness, supersession, and contradiction state,
- metacognitive coverage maps, knowledge gaps, epistemic tension records, and learning proposals,
- recall traces and consolidation runs,
- interactive probe sessions, probe turns, user correction evidence, calibration records, and memory regression tests,
- cognitive workspace frames, attention decisions, evidence anchors, atomic claims, belief states, context frames, prediction errors, salience signals, temporal episodes, replay jobs, procedure skills, simulation hypotheses, and answer-gate decisions,
- score-space definitions, score vector snapshots, score shapes, score evaluation traces, and derived scalar projections,
- self-model records, domain competence profiles, known failure patterns, self-regulation assessments, answer posture decisions, calibration aggregates, professor review traces, and self-regulation outcome records,
- Qdrant projections with rich payload metadata.

## Source Inspection Scope

This bundle was refreshed against the live repositories:

- `C:\repositories\CanDoItAll`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core`
- `C:\repositories\CanDoItAll.AgentFramework.Rag`
- `C:\repositories\CanDoItAll.AgentFramework.SemanticCompletion`

The CodeAnalytics snapshot used for the main CanDoItAll source inspection was `snap-20260515230800-1b0ae250`, scoped to composition, infrastructure, Workbench, Processes, Automation, SchedulerPlanner, AgentFramework, and SharedKernel projects. Separate snapshots were taken for the RAG driver, Qdrant driver, and SemanticCompletion driver. Since the first architecture pass, the source/MAF boundary and projection-boundary prerequisite bundles were implemented and validated; Cognitive Memory implementation itself has still not started.

The 2026-05-16 execution-control review also used CodeAnalytics snapshot `snap-20260516150857-2c8fb8f3`, focused on `CanDoItAll.AgentFramework.Maf`, `CanDoItAll.AgentFramework.Core`, source snapshot providers, Workbench, Processes, Automation, SchedulerPlanner, Infrastructure, SharedKernel, and relevant test projects.

Additional inspection of the supplied current code confirms that Workbench, Process, and Workflow source snapshot providers are already present and registered; integration tests validate deterministic paging, redaction, restricted hash policy, and Workbench z-index metadata extraction. Implementation is now proceeding phase by phase from this bundle: module foundation, common guardrails, score geometry, neuro claim/evidence foundation, source ingestion, SemanticCompletion/RAG adapter boundaries, durable taxonomy/projection lifecycle, cognitive workspace/attention routing, prediction-error/salience signal foundations, staged recall orchestration, consolidation engine foundations, temporal replay scheduler foundations, procedural skill memory/simulation foundations, and human review UI are complete.

## Existing Building Blocks Found

- CanDoItAll targets `.NET 10` and already has modular runtime composition, EF model configuration discovery, switchable database profiles, storage drivers, relational search indexing, Workbench project structures, process/workflow runtime persistence, workflow executors, and MAF agent runtime integration.
- Workbench/project structure stores project nodes, links, metadata, notes, references, and 2D layout coordinates. Z must start as metadata unless a later Workbench migration makes it first-class.
- The uploaded current code now has an explicit MAF context contribution boundary (`IAgentContextContributor`, contribution policy/results, and trace collector). Cognitive Memory should consume this boundary instead of adding more private MAF context-provider code. The old keyword-scored `WorkspaceMemoryContextProvider` remains useful as compatibility fallback only.
- The RAG repository has provider-neutral `IRagDriver`, `IRagEmbeddingGenerator`, typed filters, payload index contracts, delete-by-filter projection cleanup, capability discovery, and a Qdrant driver. It remains a projection backend, not the memory model.
- The SemanticCompletion repository has ONNX/local hashing embeddings with stable profile metadata, vector similarity, semantic ranker/classifier, intent registry, sandbox, tests, and benchmark support. It is a semantic utility, not the memory model.

## Critical Architecture Corrections

- Do not implement Cognitive Memory by adding another private provider inside `MafAgentRuntime.Capabilities.Context.cs`. Consume the existing explicit context-contribution boundary (`IAgentContextContributor` and related policy/result contracts) so Cognitive Memory can plug into MAF without making MAF own durable memory semantics.
- Do not make `CanDoItAll.Modules.CognitiveMemory` directly responsible for every source module. Keep the durable core clean and add source adapters for Workbench, Processes, Workflows, plugins, RAG, and SemanticCompletion.
- Do not make SemanticCompletion own canonicalization or relations. Wrap it behind embedding, ranking, and classification adapters.
- Do not require named vectors in V1. Use typed multi-collection projection if the RAG driver has not yet been extended.
- Do not treat high semantic similarity as identity. Spatial, graph, scope, source confidence, and human validation can override semantic similarity.
- Do not plan distributed idle compute until local deterministic job packets, leases, hashes, and acceptance validation work.
- Do not reduce Epistemic Drive to a scalar priority score. Preserve vector dimensions, evidence refs, Pareto/category/ROI metadata, project-direction intersections, and explanation text.
- Do not allow any behavior-affecting scoring surface to use only add/subtract sub-scores or a final scalar. Recall rank, attention routing, belief state, replay priority, answer confidence, probing assessment, and cross-project promotion must go through score geometry.
- Do not model Cognitive Self-Regulation as prompt persona, consciousness, emotion, or autonomous ego. It is structured control state over evidence, calibration, risk, posture, and policy.
- Do not let self-model, professor review, salience, prediction error, probing feedback, or generated summaries directly create canonical truth.
- Do not let answer posture selection use display confidence as the decision model.
- Do not run external learning or promote high-impact learning outputs without human/policy approval and source refs.
- Do not let interactive probing mutate authoritative truth directly. Probe feedback creates evidence, correction candidates, review items, regression tests, and learning signals; durable memory changes still pass through Cognitive Memory authority services and review policy.
- Do not treat `RecallContextPack` as working memory. Working memory is an active scoped workspace frame; a context pack is only rendered output.
- Do not let canonical memory items hide claim-level contradictions, scope limits, weak evidence, or temporal validity differences.
- Do not expose public direct upsert semantics for authoritative memory. Use mutation authority with idempotency, evidence, concurrency, review, audit, and projection invalidation.
- Do not let prediction errors, salience signals, replay output, simulation output, distributed workers, or answer-gate decisions directly create truth.

## Bundle Layout

- `inputs/` raw request, source artifacts, and structured input
- `analysis/` current state, corrections, assumptions, risks, and refactor decision
- `requirements/` normalized requirements and acceptance criteria
- `architecture/` target architecture, boundaries, modes, scale, security, UI, Epistemic Drive, Interactive Memory Probing, regression/calibration, Cognitive Self-Regulation, and integration notes
- `plan/` execution order, dependencies, critical foundations, and gates
- `traceability/` requirement-to-subbundle mapping
- `shared-prompts/` reusable implementation and QA prompts
- `subbundles/` numbered execution-ready workstreams
- `checklists/` implementation-control workbook and checklist rules
- `reviews/` bundle self-review and execution report seed

## Recommended Reading Order

1. `analysis/01-current-state.md`
2. `analysis/02-assumptions-and-risks.md`
3. `analysis/03-prerequisite-refactor-decision.md`
4. `architecture/01-target-solution.md`
5. `architecture/02-module-boundaries.md`
6. `architecture/13-operational-modes-and-scale.md`
7. `architecture/03-memory-taxonomy-and-data-model.md`
8. `architecture/04-mindmap-processing-architecture.md`
9. `architecture/05-recall-orchestrator.md`
10. `architecture/06-consolidation-engine.md`
11. `architecture/07-qdrant-projection-design.md`
12. `architecture/08-maf-workflow-agent-integration.md`
13. `architecture/14-epistemic-drive-and-learning-orchestration.md`
14. `architecture/15-interactive-memory-probing.md`
15. `architecture/16-probing-regression-and-calibration-loop.md`
16. `architecture/17-neuro-cognitive-integration-layer.md`
17. `architecture/18-cognitive-workspace-and-attention-router.md`
18. `architecture/19-prediction-error-salience-signal-ledger.md`
19. `architecture/20-claim-evidence-belief-ledger.md`
20. `architecture/21-schema-entity-context-binding.md`
21. `architecture/22-temporal-episodic-memory-and-replay.md`
22. `architecture/23-procedural-skill-memory-and-simulation.md`
23. `architecture/24-metamemory-confidence-and-abstention.md`
24. `architecture/26-score-geometry-driver.md`
25. `architecture/27-cognitive-self-regulation-layer.md`
26. `architecture/28-self-model-and-epistemic-identity.md`
27. `architecture/29-calibration-health-and-probing-training.md`
28. `architecture/30-professor-review-and-escalation.md`
29. `plan/01-phase-plan.md`
30. `subbundles/*/README.md`
31. `validation/test-and-quality-plan.md`

## Recommended Execution Order

1. `subbundles/00-prerequisite-boundary-gate`
2. `subbundles/01-module-foundation`
3. `subbundles/01a-common-drivers-helpers-and-ef-guardrails`
4. `subbundles/01b-score-geometry-driver`
5. `subbundles/14-neuro-foundation-claim-evidence-ledger`
6. `subbundles/02-workbench-and-source-ingestion`
7. `subbundles/03-semantic-and-rag-adapters`
8. `subbundles/04-memory-taxonomy-and-projections`
9. `subbundles/15-cognitive-workspace-attention-router`
10. `subbundles/16-prediction-error-salience-signals`
11. `subbundles/05-recall-orchestrator`
12. `subbundles/06-consolidation-engine`
13. `subbundles/17-temporal-replay-scheduler`
14. `subbundles/18-procedural-skill-memory-simulation`
15. `subbundles/08-human-review-ui`
16. `subbundles/07-maf-workflow-integration`
17. `subbundles/13a-probing-core-regression-calibration`
18. `subbundles/21-cognitive-self-model`
19. `subbundles/23-calibration-health-and-probing-training`
20. `subbundles/22-self-regulation-orchestrator`
21. `subbundles/24-professor-review-escalation`
22. `subbundles/19-metamemory-abstention-calibration`
23. `subbundles/13-interactive-memory-probing-workbench`
24. `subbundles/12-epistemic-drive-engine`
25. `subbundles/25-self-regulation-ui`
26. `subbundles/26-cognitive-self-regulation-integration-closure`
27. `subbundles/10-cross-project-memory`
28. `subbundles/09-distributed-idle-compute`
29. `subbundles/20-architecture-integration-closure`
30. `subbundles/11-validation-and-architecture-closure`

Root validation closure remains named `11-validation-and-architecture-closure` for compatibility with the existing bundle. Run common helper/driver/EF guardrails first, then score geometry, then neuro claim/evidence/context/mutation foundation before any source, projection, recall, or probing implementation. Run workspace/attention and signal ledgers before recall. Run probing core before self-model and calibration health so self-regulation has evidence to learn from. Reopen answer gating after self-regulation orchestration and professor review are available, then run Dialogue Workbench, Epistemic Drive, and Self-Regulation UI before cross-project or distributed extensions. Treat cross-project memory and distributed compute as extensions after project-scoped memory safety and self-regulation closure are proven.

## Dependency And Validation Map

- Keep the mermaid dependency map, critical-subbundle notes, and phase gates current in `plan/01-phase-plan.md`.
- If the bundle is resumed after compaction or by a different agent, use this README, the active subbundle README, and `reviews/01-execution-report.md` as durable state.
- Also use `checklists/cognitive-memory-implementation-control.xlsx` as the authoritative phase ledger. Do not advance a downstream phase when the workbook and execution report disagree.

## Implementation Control

- Workbook: `checklists/cognitive-memory-implementation-control.xlsx`
- Rules: `checklists/README.md`
- Every implementation phase must update the workbook before starting, during execution, and before closure.
- The root `subbundles/` files are authoritative. The `plan/subbundles/` files are synchronized mirrors and must remain byte-equivalent when edited.
- If an agent resumes after context compaction, it must read the workbook `Summary`, `Phase Gates`, active subbundle README, and `reviews/01-execution-report.md` before making changes.
- A phase cannot be marked `Passed` without proof paths in the workbook and a matching execution-report row.

## Validation Summary

- Bundle preparation status: `Prepared after execution-control repair`
- Bundle readiness gate: `Prepared-stage validation passed after execution-control repair`
- Epistemic Drive status: `Architecture added, implementation not started`
- Interactive Memory Probing status: `Architecture added, implementation not started`
- Neuro-cognitive patch status: `Architecture integrated, implementation not started`
- Cognitive Self-Regulation status: `Architecture integrated, implementation not started`
- Execution status: `In progress - 08-human-review-ui completed`
- Subbundle gate review: `Prerequisite, module foundation, common helper guardrail, score geometry, neuro foundation, source ingestion, semantic/RAG adapter, durable taxonomy/projection, workspace/attention, prediction-error/salience, recall, consolidation, temporal replay, procedure skill/simulation, and human review UI gates passed`
- Final closure gate: `Not started`
- Browser validation analytics: `08-human-review-ui passed with Playwright and Browser plugin evidence`

## Source Notes

Neuroscience references are used as design inspiration, not as a claim of biological equivalence. The system should be described as biologically inspired cognitive memory, not as a brain simulation.
