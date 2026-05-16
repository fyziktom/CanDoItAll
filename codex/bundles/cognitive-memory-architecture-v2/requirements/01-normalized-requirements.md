# Normalized Requirements

## Functional Requirements

### FR-001: Source Ingestion

The system shall ingest knowledge from project mindmaps, workbench project objects, files, repositories, process runs, workflow runs, plugin outputs, emails, and future connectors.

### FR-002: Raw Source Provenance

The system shall preserve raw source references and content hashes for every derived memory item.

### FR-003: Canonicalization

The system shall normalize raw source items into canonical source records with typed entities, scope, tags, source refs, and confidence.

### FR-004: Memory Taxonomy

The system shall support at least these memory types:

- source,
- semantic,
- episodic,
- procedural,
- decision,
- reflection,
- working,
- metacognitive.

### FR-005: Mindmap Spatial Processing

The system shall process mindmap coordinates and relationships as first-class signals, not as decorative layout only.

### FR-006: Multi-View Similarity

The system shall combine semantic, spatial, graph, lexical, metadata, temporal, and activation signals.

### FR-007: Context-Separated Relatedness

The system shall explicitly represent records that are semantically related but intentionally separated by project context.

### FR-008: Qdrant Projection

The system shall project selected memory items into Qdrant with rich payload metadata and source/version references.

### FR-009: Rebuildable Projection

The system shall be able to rebuild Qdrant projections from durable memory state.

### FR-010: Recall Orchestration

The system shall perform staged recall: intent interpretation, coarse activation, association expansion, focus selection, detail retrieval, context-pack construction, and trace recording.

### FR-011: Working Memory

The system shall maintain task/workflow-run-specific working memory context.

### FR-012: Episodic Memory

The system shall extract episodes from process/workflow/agent execution events.

### FR-013: Procedural Memory

The system shall extract reusable procedures from successful tasks, validated workflows, and human-authored guidance.

### FR-014: Reflection

The system shall create reflection records after important agent/process/workflow runs.

### FR-015: Consolidation

The system shall run scheduled/idle consolidation jobs that refine memory, update projections, detect contradictions, and create review tasks.

### FR-016: Human Review

The system shall provide a review queue for ambiguous, high-risk, contradictory, stale, or cross-project promoted memory items.

### FR-017: MAF Integration

The system shall provide Microsoft Agent Framework context provider/tool/workflow integration.

### FR-018: Workflow Executors

The system shall expose memory operations as workflow executors.

### FR-019: Distributed Idle Compute

The system shall allow trusted LAN devices to perform deterministic memory jobs without directly mutating authoritative memory state.

### FR-020: Auditability

The system shall record recall traces, consolidation runs, projection changes, and human review decisions.

### FR-021: Explicit Operating Modes

The system shall model ingestion, canonicalization, projection, recall, consolidation, trust/write policy, and compute-placement modes as strongly typed options.

### FR-022: High-Volume Operations

The system shall support large source sets through cursors, hashes, idempotency keys, leases, bounded batches, projection state, and resumable background jobs.

### FR-023: Prerequisite Boundaries

The implementation shall consume MAF context contribution and source snapshot contracts instead of adding cognitive memory logic directly to private MAF context internals or ad hoc EF read paths.

### FR-024: Knowledge Coverage Modeling

The system shall maintain knowledge regions and coverage maps that represent topic/subtopic coverage, confidence, staleness, risk, source count, open questions, and contradiction pressure.

### FR-025: Knowledge Gap Detection

The system shall detect knowledge gap regions from recall traces, failed or reworked workflow/process runs, user corrections, contradiction records, stale records, repeated topic usage, unresolved questions, weak source coverage, probing failures, active process needs, and mindmap/project graph relevance.

### FR-026: Multi-Dimensional Knowledge Need Modeling

The system shall model knowledge need as a `KnowledgeNeedVector` with preserved dimensions including usage frequency, confidence weakness, risk impact, staleness, failure recurrence, strategic alignment, question density, business value, estimated learning effort, source availability, source quality, contradiction pressure, user interest signal, volatility, and expected reuse.

### FR-027: Explainable Learning Proposal Generation

The system shall generate human-reviewable learning proposals that include topic, subtopic coverage map, evidence summary, why it matters, uncertainty/gap explanation, related project directions, suggested sources, source trust level, estimated effort, expected outputs, proposed depth, risks, required approval, probing questions, and acceptance criteria.

### FR-028: Human Approval For Learning

The system shall allow users to approve, reject, snooze, narrow scope, expand scope, add sources, request probing first, turn a proposal into a Codex bundle, or assign the learning task to a human or approved agent.

### FR-029: Learning Workflow Orchestration

The system shall orchestrate approved learning tasks through MAF or equivalent workflow infrastructure while keeping durable memory authority inside Cognitive Memory.

### FR-030: Knowledge Probing Integration

The system shall support bidirectional integration with knowledge probing so probing can reveal gaps, Epistemic Drive can generate probing questions, probing can validate learning outcomes, and probing results can update gap evidence without becoming automatic truth.

### FR-031: Learning Outcomes

The system shall produce auditable learning outcomes with source-grounded draft canonical records, draft procedures/runbooks, non-happy-path notes, probing questions, QA findings, and coverage map updates.

## Non-Functional Requirements

### NFR-001: Deterministic Core

Critical clustering, projection planning, source diffing, and acceptance decisions should be deterministic where possible.

### NFR-002: Provenance First

No memory item may be created without at least one source reference or an explicit `system-generated` reason with evidence.

### NFR-003: Provider Independence

Embedding, LLM, vector DB, and source connector implementations must be replaceable.

### NFR-004: Offline Capability

The architecture should support local ONNX/Ollama providers and not require external API access.

### NFR-005: Incremental Processing

The system should update changed sources without recomputing everything.

### NFR-006: Safe Degradation

If Qdrant is unavailable, lexical/graph/source recall should still work.

### NFR-007: Explainability

Recall and consolidation decisions must be inspectable.

### NFR-008: Secret Safety

Secrets must not be embedded, summarized, or injected into external model contexts.

### NFR-009: Versioning

Every projection, embedding, classifier, clustering result, and generated summary must store algorithm/model version information.

### NFR-010: Performance

The module should prioritize interactive recall latency and move heavy clustering/consolidation work to idle/background jobs.

### NFR-011: No Silent Truncation

Recall, source scans, and consolidation jobs must record budget exclusions, skipped channels, failed providers, and unavailable projections in durable traces.

### NFR-012: Idempotent Mutations

All mutating memory operations must use deterministic identities that include source item keys, content hashes, algorithm versions, embedding profiles, and projection profiles.

### NFR-013: Boundary Stability

MAF, Workbench, Process, Workflow, RAG, and SemanticCompletion integrations must depend on explicit contracts and adapters so Cognitive Memory can evolve without direct coupling to private implementation details.

### NFR-014: No Scalar-Only Epistemic Scoring

Epistemic Drive must not collapse knowledge need into a single authoritative score. Any display priority score must be secondary to preserved vector components, evidence refs, category, Pareto rank, ROI estimate, and explanation.

### NFR-015: Learning Approval Safety

External source study and high-impact memory updates must be approval-gated according to policy.

### NFR-016: Source-Grounded Learning Outputs

Learning-derived canonical records and procedures must require source refs and remain draft until validated.

### NFR-017: Auditable Learning Decisions

Learning proposals, approval decisions, learning tasks, outcomes, QA findings, and promotion decisions must be auditable.

### NFR-018: Idempotent And Resumable Learning Processing

Epistemic scans and learning tasks must use deterministic input hashes, source versions, algorithm versions, leases, and retry-safe writes.

### NFR-019: Projection Boundary Preservation

Qdrant/search projections may accelerate learning proposal discovery or UI search, but they must never be the only source of knowledge gaps, proposals, learning outcomes, or source truth.

## Constraints

- The module must fit the existing CanDoItAll modular architecture.
- It must use existing EF registration patterns.
- It must reuse existing storage drivers.
- It must reuse or wrap the existing RAG and SemanticCompletion modules.
- It must integrate with Microsoft Agent Framework workflows and tools.
- It must allow future plugin-based sources.
- It must preserve raw evidence and avoid treating generated summaries as source truth.
- It must keep Epistemic Drive local-first and compatible with offline operation.
- It must prevent project-private evidence leakage when aggregating cross-project learning opportunities.

### FR-032: Interactive Memory Probe Sessions

The system shall support durable, project-scoped interactive memory probe sessions with modes such as free dialogue, guided exam, gap hunting, contradiction hunt, context-separation drill, procedure drill, source audit, learning validation, and regression replay.

### FR-033: Probe Turns With Recall Traces

Every probe answer shall be backed by a recall request and a persisted recall trace. Probe turns shall store answer metadata, confidence, source refs, warnings, findings, and access/redaction outcomes.

### FR-034: Probe Feedback And User Corrections

The system shall allow users to confirm, correct, mark missing knowledge, mark wrong scope, request sources, create review items, create regression tests, request learning proposals, snooze, or ignore probe outcomes.

### FR-035: Probe Question Generation

The system shall generate probing questions from coverage maps, knowledge gaps, stale records, contradictions, active project directions, recall failures, context-separation candidates, user interest signals, and controlled serendipity.

### FR-036: Probe Evidence Integration

Probe outcomes shall become typed evidence for knowledge gaps, coverage maps, confidence calibration, contradictions, supersession candidates, and Epistemic Drive proposals without automatically mutating active memory.

### FR-037: Memory Regression Tests From Probe Failures

The system shall convert important or failed probe turns into durable regression test cases with expected constraints, required/forbidden memory refs, source requirements, evaluator profile, and replay results.

### FR-038: Confidence Calibration From Probing

The system shall track confidence-vs-outcome calibration evidence, especially high-confidence rejected answers, low-confidence confirmed answers, wrong-scope answers, missing-source answers, and redaction-limited answers.

### NFR-020: No Direct Truth Mutation From Probing

Probe feedback and user corrections must not directly overwrite active canonical memory. They must create evidence, review items, regression tests, or draft candidates according to policy.

### NFR-021: Probe Traceability

Probe sessions, turns, feedback, findings, review items, regression tests, and Epistemic Drive evidence must be traceable to each other.

### NFR-022: Probe Privacy And Redaction

Probe transcripts and correction data must obey project access policy, retention policy, redaction policy, and external-provider export restrictions.

### NFR-023: Probe Replayability

Regression tests created from probes must be replayable and must store enough input, access context, evaluator profile, and expected constraints to diagnose failures.

### NFR-024: Probe Diversity With Control

Question generation may use randomness for coverage and serendipity, but it must preserve evidence, reason, access filtering, and deterministic replay metadata for generated question sets.
