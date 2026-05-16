# Architecture Gap Review

## Executive Finding

The current architecture is a strong cognitive-memory foundation, but it still behaves mostly like a disciplined enterprise memory/RAG system with consolidation, probing, and learning proposals. To become more brain-like in useful engineering terms, it needs explicit mechanisms for:

- working memory as a temporary active cognitive workspace,
- attention/routing as an executive control layer,
- prediction error as a learning signal,
- salience/novelty/risk/usefulness as durable cognitive signals,
- claim-level evidence and belief revision,
- entity/context binding before semantic merging,
- temporal episodic sequence and causality,
- prioritized replay/rehearsal,
- procedural skill formation,
- answer-time metamemory and abstention.

These are architecture additions. They should not weaken existing governance or source truth rules.

## What Is Already Good

### Durable Memory Boundary

The existing bundle correctly states that Qdrant/RAG is only a projection. This is essential and should remain untouched.

### Source Truth And Provenance

The design already protects source manifests, source items, and source references. Generated summaries are correctly not treated as source truth.

### Interactive Probing

The existing probing layer is strong because it treats human corrections as evidence, not automatic truth. It also adds regression and calibration, which is an important architecture step.

### Epistemic Drive

The current Epistemic Drive design correctly rejects scalar-only priority. It preserves vector dimensions, evidence, ROI, Pareto/category metadata, and review actions.

## Core Weaknesses

### 1. Working Memory Is Under-Specified

The architecture mentions working memory and context packs, but there is no explicit active workspace model.

A context pack is a rendered output. Working memory is an active, mutable, short-lived control structure with:

- focus slots,
- current goals,
- active task/process state,
- recently activated claims,
- inhibited distractors,
- unresolved questions,
- source sufficiency state,
- token/context budget,
- attention weights,
- expiry and optional episodic persistence.

Without this, recall can retrieve good candidates but the system has no rigorous model for what it is currently thinking about.

### 2. Attention Is Mixed Into Recall

The recall orchestrator has focus selection, but attention should be a separate executive decision layer. Attention decides whether the next action is recall, probing, source audit, clarification, consolidation, learning proposal, or abstention.

Recall answers the question: "What memory is relevant?"  
Attention answers: "What cognitive operation should happen now?"

### 3. Prediction Error Is Not First-Class

The current architecture has contradictions, probing failures, confidence calibration, and workflow failures, but these are not unified as prediction-error events.

A cognitive system should record:

- what it expected,
- what actually happened,
- how large the error was,
- whether the error was caused by missing knowledge, bad retrieval, stale source, wrong scope, overconfidence, redaction, or real contradiction,
- which future behavior should adapt.

Prediction error should drive Epistemic Drive, replay scheduling, confidence calibration, and procedural improvement.

### 4. Salience Signals Are Not Durable Enough

Activation exists, but activation is a derived state. The architecture should store the underlying signals:

- novelty,
- surprise,
- risk,
- usefulness,
- recurrence,
- user interest,
- workflow rework cost,
- validation success,
- contradiction pressure,
- strategic alignment.

These signals should remain queryable and auditable instead of being reduced to a single activation score.

### 5. Canonical Memory Items Are Too Coarse For Belief Revision

`MemoryItem` and `CanonicalMemoryItemRecord` are useful containers, but they are not enough to safely represent enterprise knowledge.

A single memory item may contain multiple claims with different evidence strength, different scopes, and different time validity. If contradictions are tracked only between memory items, the system can hide conflicts inside summaries.

The architecture needs atomic claims and evidence relations.

### 6. Evidence References Need Source Anchors

Existing evidence refs identify source items, but source grounding should support fine anchors:

- storage locator,
- file/repository path,
- source item id,
- character span or structured node path,
- quote hash,
- source trust level,
- redaction state,
- validity window.

Without anchors, "source-backed" may still be too vague for review and regression tests.

### 7. Entity And Context Binding Is Not First-Class

The existing design uses tags, scopes, project graph, and context-separated relations. That is good, but the system still needs a binding step before semantic merging:

- entity registry,
- aliases,
- environment/context frames,
- role/process step frames,
- branch/version/platform frames,
- typed context boundaries.

This prevents mistakes such as mixing production Docker deployment with test Docker simulation.

### 8. Episodic Memory Lacks Sequence/Causality

Process and workflow runs are excellent inputs, but the architecture should explicitly encode episodes as sequences with causality:

- event order,
- actor/agent/role,
- decision point,
- artifact produced,
- expected outcome,
- actual outcome,
- error/rework,
- follow-up,
- validity in time.

This is needed for answers such as "why was this decision made?" or "what failed before?".

### 9. Replay/Rehearsal Is Not Specific Enough

Consolidation exists, but replay should be scheduled based on cognitive signals:

- high prediction error,
- high risk,
- high use,
- high novelty,
- stale important source,
- low-confidence but recurring recall,
- high-value procedural skill.

Replay should support spaced retrieval and regression replay, not only nightly summarization.

### 10. Procedural Memory Is Too Thin

The current procedure extraction direction is useful, but a true procedural memory needs:

- triggers,
- preconditions,
- postconditions,
- step graph,
- required tools/plugins,
- failure modes,
- rollback/compensation,
- success metrics,
- validation evidence,
- skill maturity,
- automation binding,
- last successful execution.

Textual runbooks are not enough.

### 11. Metamemory Is Mostly Post-Hoc

The current calibration loop records confidence outcomes, but the architecture needs an answer-time gate:

- answer normally,
- answer with warnings,
- ask for clarification,
- run source audit,
- request probing,
- propose learning,
- abstain.

This is essential for safe agent behavior.

### 12. Mutation Semantics Are Too Direct

The current `IMemoryStore` exposes `UpsertMemoryItemAsync` and `UpsertRelationAsync`. For architecture sketches this is acceptable, but the final design should add a memory mutation authority so all writes pass through:

- idempotency key,
- source/evidence check,
- optimistic concurrency,
- validation/review policy,
- audit event,
- projection invalidation.

Direct upsert contracts are risky in a multi-agent, probing, learning, and consolidation system.

## Recommended Architecture Decision

Add a new document set called **Neuro-Cognitive Integration Layer**. It should sit between ingestion/recall/probing/consolidation and the durable memory stores.

This layer should not be mystical or biological in implementation. It is an engineering abstraction:

- working memory = active scoped frame,
- attention = operation router,
- salience = event-sourced signal vector,
- prediction error = expected vs actual comparison,
- belief = claim/evidence state,
- replay = scheduled reprocessing/rehearsal,
- procedural skill = validated executable procedure graph,
- metamemory = answer decision gate.
