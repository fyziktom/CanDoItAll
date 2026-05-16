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

### FR-039: Cognitive Workspace Frames

The system shall represent temporary active working memory as scoped workspace frames with focus slots, goal stack, inhibited candidates, open questions, context budget, cognitive load, and expiry.

### FR-040: Attention Router

The system shall have an explicit attention router that chooses the next cognitive operation: recall, answer from workspace, ask clarification, source audit, probe, review, learning proposal, replay, or abstention.

### FR-041: Claim/Evidence/Belief Ledger

The system shall support atomic memory claims with source evidence anchors, support/attack evidence, scope/context frames, confidence, validation state, temporal validity, and belief state.

### FR-042: Evidence Anchors

The system shall support fine-grained evidence anchors including source ids, storage locators, structured paths, text spans, quote hashes, trust level, redaction state, and source version/hash.

### FR-043: Memory Mutation Authority

Authoritative memory changes shall pass through a mutation authority with idempotency, optimistic concurrency, actor identity, evidence checks, review policy, audit events, and projection invalidation.

### FR-044: Schema, Entity, And Context Binding

The system shall resolve entities, aliases, schemas, and context frames before semantic merging or claim promotion. Context boundaries must prevent substituting semantically similar but operationally incompatible memories.

### FR-045: Prediction Expectations And Prediction Errors

The system shall record expected outcomes and observed mismatches for important probe turns, workflow runs, procedure executions, QA events, and high-risk answers.

### FR-046: Salience Signal Ledger

The system shall persist multi-dimensional cognitive signals such as novelty, surprise, risk, usefulness, reward, rework cost, contradiction pressure, user interest, staleness pressure, source weakness, and calibration risk.

### FR-047: Temporal Episodic Memory

The system shall represent episodes as ordered sequences with actors, steps, decisions, artifacts, expected outcomes, actual outcomes, prediction errors, related claims, and related procedures.

### FR-048: Replay/Rehearsal Scheduler

The system shall schedule replay jobs using cognitive signals, prediction errors, risk, staleness, usefulness, user interest, contradiction pressure, and procedure maturity.

### FR-049: Procedural Skill Memory

The system shall represent procedures as skill records with preconditions, steps, postconditions, failure modes, validation evidence, maturity, risk, automation binding, and source anchors.

### FR-050: Simulation Sandbox

The system shall support speculative simulation/planning outputs for procedure alternatives and cross-project analogies. Simulation outputs must be clearly marked as hypotheses and cannot become authoritative without review.

### FR-051: Metamemory Answer Gate

The system shall evaluate answer readiness before rendering answers using source sufficiency, context fit, belief state, confidence calibration, contradiction risk, staleness, redaction, risk level, and access policy.

### FR-052: Workspace-Aware Probing

Probe sessions shall attach to or create workspace frames, publish prediction errors and salience signals, and create claim-level correction candidates without directly mutating authoritative truth.

### FR-053: Generic Score Geometry Driver

The system shall provide a reusable score geometry driver with typed score spaces, dimension definitions, vector snapshots, shapes/regions, normalization profiles, missing-dimension policy, scalar projection policy, and evaluation traces.

### FR-054: Score Geometry Consumption

Recall ranking, attention routing, belief-state calculation, salience consumption, replay scheduling, probe assessment, answer gating, Epistemic Drive, mindmap similarity, and cross-project promotion shall use declared score spaces and preserve score vectors/shapes instead of relying on local scalar-only formulas.

### NFR-025: No Direct Public Upsert For Authoritative Memory

Public write operations must use mutation authority. Repository-style upsert methods may exist internally only.

### NFR-026: No Silent Claim Merge

Claims with different context frames, validity windows, or evidence state must not be silently merged into one canonical truth.

### NFR-027: No Scalar-Only Salience

Salience must preserve signal dimensions. A display priority score may exist only as derived UI data.

### NFR-028: Explainable Attention

Attention decisions must include structured reasons and be persisted in recall/probe traces where relevant.

### NFR-029: Replay Safety

Replay jobs may produce draft changes, review items, regression results, or projection invalidations, but must not directly promote authoritative truth.

### NFR-030: Speculation Labeling

Simulation, analogy, and associative exploration outputs must be labeled speculative until source-backed and reviewed.

### NFR-031: Answer Abstention Safety

The answer gate must support abstention and clarification. The system must not hide uncertainty behind fluent wording.

### NFR-032: Context Boundary Safety

Context boundaries must be evaluated before answer rendering and procedure execution.

### NFR-033: Auditability Of Cognitive Signals

Signals, prediction errors, attention decisions, and answer gate decisions must be traceable to evidence, actor, time, and algorithm/profile version.

### NFR-034: No Scalar-Only Behavior Scoring

No behavior-affecting decision may store only a final score, priority, confidence, weight, or untyped score breakdown. Scalar projections are allowed only as derived display, sorting, queue, or tie-breaker data backed by score evaluation traces.

### NFR-035: Versioned Score Spaces

Every score vector, shape, scalar projection, and evaluation trace must record score space kind, schema version, normalization profile, algorithm version, evidence refs, and missing-dimension behavior.

### NFR-036: Score Geometry Queryability And Performance

Query-critical score dimensions must be relational/indexable, while full vector/shape payloads must be bounded and versioned. Hot score evaluation paths must avoid dictionary-heavy allocation patterns and repeated vector-array copies.

### FR-055: Cognitive Self-Model

The system shall define a durable, scoped, evidence-backed self-model containing operating principles, allowed task categories, restricted task categories, domain competence profiles, weak domains, known failure patterns, and default self-regulation policy.

### FR-056: Self-Regulation Assessment

The system shall evaluate important answers, tool actions, workflow actions, probes, reviews, and memory mutation requests through a self-regulation assessment that includes workspace state, self-model, competence profiles, calibration health, known failure pattern matches, score trace, warnings, and required operations.

### FR-057: Humility Trigger Engine

The system shall define a reusable humility trigger engine that detects conditions requiring reduced confidence, caveats, clarification, source audit, probing, review, professor review, or abstention.

### FR-058: Answer Posture Selection

The system shall select an explicit answer posture before answer rendering. Supported postures include direct confident, direct with caveats, preliminary reaction, hypothesis, clarification question, source audit request, probe question, review required, professor review required, and abstain.

### FR-059: Calibration Health Aggregates

The system shall aggregate calibration evidence by domain, task type, model profile, risk category, and feature pattern. Required metrics include expected calibration error or equivalent, Brier score or squared calibration loss, signed confidence bias, overconfidence rate, underconfidence rate, abstention quality, wrong-scope rate, and source-insufficient rate.

### FR-060: Professor Review Escalation

The system shall support escalation to a professor review service for challenge, contradiction hunt, architecture review, calibration review, source sufficiency review, alternative hypothesis review, failure mode review, and learning expansion.

### FR-061: Post-Outcome Self-Regulation Feedback

The system shall convert answer, probe, workflow, review, and professor-review outcomes into calibration records, prediction errors, salience signals, regression candidates, probing drills, failure pattern updates, review items, replay jobs, and self-model update proposals.

### NFR-037: Self-Regulation Auditability

Every behavior-affecting self-regulation decision shall preserve evidence refs, score evaluation trace, algorithm/profile version, actor/model profile, and timestamp.

### NFR-038: Non-Anthropomorphic Self-Regulation Safety

The architecture shall not describe Self-Regulation as consciousness, emotional simulation, or autonomous ego. It shall describe it as calibrated agency and epistemic control.

### NFR-039: Calibration Profile Versioning

Calibration and self-model profile changes shall be versioned. Old traces must not be reinterpreted by new profiles without migration or recalculation.

### NFR-040: Professor Review Governance

Professor review shall not bypass source truth, access policy, redaction, mutation authority, human review, or safety policy.

### NFR-041: No Scalar-Only Self-Regulation

Self-regulation assessment, answer posture selection, professor-review routing, and calibration health shall use score geometry traces. Scalar display confidence is allowed only as a rendering/projection aid.
