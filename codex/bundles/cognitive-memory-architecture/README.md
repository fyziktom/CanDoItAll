# CanDoItAll Cognitive Memory Architecture Bundle

## Profile

- `initiative`

## Mission

Design a biologically inspired, enterprise-grade Cognitive Memory module for CanDoItAll. The module must behave less like a flat RAG index and more like a disciplined memory system: coarse associative recall first, focused attention second, detail retrieval third, and idle-time consolidation afterwards.

This bundle is architecture and planning only. It does not authorize implementation of the Cognitive Memory module.

## Outcome Contract

- Requested outcome: a validated architecture bundle that another implementation agent can execute phase by phase without rediscovering the memory model, data ownership, integration boundaries, or proof obligations.
- Hard constraints: Qdrant is a rebuildable projection, raw source provenance is mandatory, generated summaries are not source truth, MAF is executive control rather than durable memory storage, and distributed workers cannot mutate authoritative memory.
- Evidence required before closure: source-backed architecture updates, dependency-aware subbundles, explicit progression gates, traceability rows, current-source inspection notes, and a separate prerequisite-refactor bundle if existing code must change before implementation starts.
- Known blockers or explicit scope exceptions: no implementation is included; build/test proof is not required for this architecture repair; the prerequisite MAF/source-boundary refactor must be approved before the Cognitive Memory implementation starts.

## Key Decision

Qdrant/RAG is not the memory. It is a rebuildable projection layer. The durable memory is the combination of:

- raw source manifests and immutable source references,
- canonical memory items,
- explicit memory graph relations,
- episodic records from process, workflow, and agent execution,
- procedural records extracted from successful work,
- activation, confidence, staleness, supersession, and contradiction state,
- recall traces and consolidation runs,
- Qdrant projections with rich payload metadata.

## Source Inspection Scope

This bundle was refreshed against the live repositories:

- `C:\repositories\CanDoItAll`
- `C:\repositories\CanDoItAll.AgentFramework.Rag`
- `C:\repositories\CanDoItAll.AgentFramework.SemanticCompletion`

The CodeAnalytics snapshot used for the main CanDoItAll source inspection was `snap-20260515230800-1b0ae250`, scoped to composition, infrastructure, Workbench, Processes, Automation, SchedulerPlanner, AgentFramework, and SharedKernel projects. Separate snapshots were taken for the RAG driver, Qdrant driver, and SemanticCompletion driver. Since the first architecture pass, the source/MAF boundary and projection-boundary prerequisite bundles were implemented and validated; Cognitive Memory implementation itself has still not started.

## Existing Building Blocks Found

- CanDoItAll targets `.NET 10` and already has modular runtime composition, EF model configuration discovery, switchable database profiles, storage drivers, relational search indexing, Workbench project structures, process/workflow runtime persistence, workflow executors, and MAF agent runtime integration.
- Workbench/project structure stores project nodes, links, metadata, notes, references, and 2D layout coordinates. Z must start as metadata unless a later Workbench migration makes it first-class.
- The current MAF runtime has useful context provider hooks, Mem0 support, workflow executors, process/project-structure tools, and workspace memory, but its context provider composition is private and hardwired.
- The RAG repository has provider-neutral `IRagDriver`, `IRagEmbeddingGenerator`, typed filters, payload index contracts, delete-by-filter projection cleanup, capability discovery, and a Qdrant driver. It remains a projection backend, not the memory model.
- The SemanticCompletion repository has ONNX/local hashing embeddings with stable profile metadata, vector similarity, semantic ranker/classifier, intent registry, sandbox, tests, and benchmark support. It is a semantic utility, not the memory model.

## Critical Architecture Corrections

- Do not implement Cognitive Memory by adding another private provider inside `MafAgentRuntime.Capabilities.Context.cs`. First introduce an explicit context-contribution boundary so Cognitive Memory can plug into MAF without making MAF own durable memory semantics.
- Do not make `CanDoItAll.Modules.CognitiveMemory` directly responsible for every source module. Keep the durable core clean and add source adapters for Workbench, Processes, Workflows, plugins, RAG, and SemanticCompletion.
- Do not make SemanticCompletion own canonicalization or relations. Wrap it behind embedding, ranking, and classification adapters.
- Do not require named vectors in V1. Use typed multi-collection projection if the RAG driver has not yet been extended.
- Do not treat high semantic similarity as identity. Spatial, graph, scope, source confidence, and human validation can override semantic similarity.
- Do not plan distributed idle compute until local deterministic job packets, leases, hashes, and acceptance validation work.

## Bundle Layout

- `inputs/` raw request, source artifacts, and structured input
- `analysis/` current state, corrections, assumptions, risks, and refactor decision
- `requirements/` normalized requirements and acceptance criteria
- `architecture/` target architecture, boundaries, modes, scale, security, UI, and integration notes
- `plan/` execution order, dependencies, critical foundations, and gates
- `traceability/` requirement-to-subbundle mapping
- `shared-prompts/` reusable implementation and QA prompts
- `subbundles/` numbered execution-ready workstreams
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
13. `plan/01-phase-plan.md`
14. `subbundles/*/README.md`
15. `validation/test-and-quality-plan.md`

## Recommended Execution Order

1. `subbundles/00-prerequisite-boundary-gate`
2. `subbundles/01-module-foundation`
3. `subbundles/02-workbench-and-source-ingestion`
4. `subbundles/03-semantic-and-rag-adapters`
5. `subbundles/04-memory-taxonomy-and-projections`
6. `subbundles/05-recall-orchestrator`
7. `subbundles/06-consolidation-engine`
8. `subbundles/07-maf-workflow-integration`
9. `subbundles/08-human-review-ui`
10. `subbundles/09-distributed-idle-compute`
11. `subbundles/10-cross-project-memory`
12. `subbundles/11-validation-and-architecture-closure`

## Dependency And Validation Map

- Keep the mermaid dependency map, critical-subbundle notes, and phase gates current in `plan/01-phase-plan.md`.
- If the bundle is resumed after compaction or by a different agent, use this README, the active subbundle README, and `reviews/01-execution-report.md` as durable state.

## Validation Summary

- Bundle preparation status: `Prepared after prerequisite sync`
- Bundle readiness gate: `Prepared-stage validation passed`
- Execution status: `Not started`
- Subbundle gate review: `Prerequisite gates passed`
- Final closure gate: `Not started`
- Browser validation analytics: `Planned only`

## Source Notes

Neuroscience references are used as design inspiration, not as a claim of biological equivalence. The system should be described as biologically inspired cognitive memory, not as a brain simulation.
