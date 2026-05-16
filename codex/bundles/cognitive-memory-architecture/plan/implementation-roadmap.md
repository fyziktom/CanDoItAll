# Implementation Roadmap

## Strategy

Implement Cognitive Memory as a module with strict boundaries. The first version should create durable memory records and make recall explainable. The system can become more brain-like over time, but V1 must avoid overfitting to vector search or LLM-generated summaries.

## Phase 0: Architecture Alignment

Deliverables:

- approve memory taxonomy,
- approve source-of-truth hierarchy,
- approve Qdrant-as-projection principle,
- approve MAF/workflow integration points,
- approve EF model names and module boundaries.

Exit criteria:

- CTO/architect review complete,
- no unresolved source-of-truth ambiguity,
- accepted migration strategy for X/Y/Z mindmap coordinates.

## Phase 1: Module Foundation and EF Models

Deliverables:

- new module project,
- EF entities/configurations,
- repositories,
- service registration,
- basic UI/API list/detail endpoints,
- test database setup.

Recommended initial tables:

- source manifests,
- source items,
- canonical records,
- memory items,
- memory relations,
- projections,
- recall traces,
- consolidation runs,
- review items.

Exit criteria:

- module loads in CanDoItAll,
- migrations apply,
- CRUD tests pass,
- memory item detail can show source refs.

## Phase 2: Workbench/Mindmap Source Ingestion

Deliverables:

- `ProjectObjectRecord` source adapter,
- `ProjectObjectLinkRecord` source adapter,
- X/Y/Z coordinate extraction,
- canonicalization for nodes and links,
- source hash/change detection.

Exit criteria:

- existing project mindmap can be ingested,
- source items include layout and graph data,
- no duplicate source items after repeated ingest.

## Phase 3: Semantic and RAG Integration

Deliverables:

- embedding provider adapter over existing SemanticCompletion/SemanticKernel driver,
- projection builder,
- Qdrant projection adapter over existing RAG driver,
- payload filter model,
- projection status model.

Exit criteria:

- memory items can be embedded and upserted,
- projection can be rebuilt,
- filtered search works by project/type/scope/tags,
- fallback works when Qdrant is unavailable.

## Phase 4: Recall Orchestrator

Deliverables:

- intent classification,
- lexical + vector + graph candidate retrieval,
- activation/confidence scoring,
- context-pack builder,
- recall trace persistence.

Exit criteria:

- recall returns context packs,
- trace viewer shows stages,
- Docker production vs Docker testing example is correctly separated,
- access policy redaction works.

## Phase 5: Process/Workflow Episodic Memory

Deliverables:

- source adapters for process/workflow runs,
- episode extraction,
- decision memory extraction,
- reflection source events,
- post-run hook or scheduled job.

Exit criteria:

- completed process/workflow produces memory events,
- episodes link to artifacts and decisions,
- generated episode memory is traceable.

## Phase 6: Consolidation Engine

Deliverables:

- consolidation run model,
- changed source scan,
- relation detection,
- contradiction detection,
- supersession/staleness updates,
- Qdrant projection updates,
- run reports.

Exit criteria:

- manual consolidation works,
- scheduled/idle consolidation works,
- review items are created for ambiguous cases,
- consolidation report is stored and inspectable.

## Phase 7: MAF and Workflow Integration

Deliverables:

- cognitive memory MAF context provider,
- memory tools,
- workflow executors,
- context-pack injection policy,
- recall trace link to agent runs.

Exit criteria:

- an agent workflow can request memory recall,
- a workflow node can execute consolidation,
- agent output stores recall trace and reflection source event.

## Phase 8: Human Review UI and Procedure Library

Deliverables:

- human review queue,
- approve/reject/split/merge actions,
- procedure library UI,
- memory item source/relations view.

Exit criteria:

- review decisions update memory state,
- high-risk procedures require approval,
- procedure can create workflow/process action.

## Phase 9: Distributed Idle Compute

Deliverables:

- job packet model,
- coordinator,
- worker registration,
- deterministic worker outputs,
- validation/acceptance pipeline.

Exit criteria:

- worker can process safe jobs,
- coordinator validates hashes and accepts/rejects output,
- workers cannot mutate DB or Qdrant directly.

## Phase 10: Cross-Project Knowledge

Deliverables:

- global topic collection,
- cross-project topic promotion,
- project-to-global relation model,
- review queue for cross-project promotion.

Exit criteria:

- global Docker topic can be built from multiple projects,
- project-specific context remains preserved,
- cross-project recall can be scoped and explained.

## Phase 11: Epistemic Drive And Learning Orchestration

Deliverables:

- knowledge region and project direction models,
- knowledge coverage maps,
- knowledge gap records with evidence refs,
- `KnowledgeNeedVector` persistence,
- epistemic tension evaluation with Pareto/category/ROI metadata,
- human-reviewable learning proposals,
- probing question generation,
- approval-gated learning task planning,
- learning outcome reports with draft canonical/procedure/probing outputs,
- Night Reflection / Cognitive Briefing UI.

Dependencies:

- source ingestion,
- memory taxonomy and durable records,
- recall traces and feedback,
- consolidation engine,
- human review UI,
- MAF workflow integration,
- probing dialog contracts when available,
- optional cross-project memory for global gap aggregation.

Exit criteria:

- Docker operational knowledge fixture produces a proposal with subtopic coverage, evidence refs, project direction intersections, suggested sources, effort estimate, probing questions, and approval actions,
- vector dimensions are preserved and tested,
- proposals cannot execute external study without required approval,
- learning-derived canonical records require source refs and remain draft until validated,
- projection refresh happens only after durable memory updates.

## First Vertical Slice

Recommended first implementation slice:

```text
Workbench nodes -> source items -> canonical memory -> Qdrant projection -> recall -> trace viewer
```

This slice proves the core value while avoiding early complexity from distributed compute and full process reflection.

After the first recall/consolidation/review path is stable, add the Epistemic Drive vertical slice:

```text
Recall/consolidation evidence -> coverage map -> gap region -> epistemic tension vector -> learning proposal -> human decision -> planned learning task
```
