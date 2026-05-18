# Operational Modes And Scale

## Purpose

Cognitive Memory will run in different modes and over large data volumes. The architecture must define mode behavior before implementation so recall, ingestion, consolidation, projection, and review do not become a set of unrelated services with hidden side effects.

## Mode Families

| Family | Modes | Owner |
|---|---|---|
| Source ingestion | `SnapshotScan`, `IncrementalScan`, `OnDemandItem`, `ConnectorBackfill`, `ReplayFromStorage` | Source scanner and source adapters |
| Canonicalization | `DeterministicOnly`, `LlmDraft`, `LlmReviewRequired`, `HumanAuthored` | Canonicalization engine |
| Projection | `Disabled`, `RelationalSearchOnly`, `QdrantSingleVector`, `QdrantMultiCollection`, `NamedVectorsFuture` | Projection manager |
| Recall | `QuickAssociative`, `FocusedTaskContext`, `DeepSourceGrounded`, `ProcedureLookup`, `DecisionLookup`, `IncidentLearning`, `CrossProjectAnalogy` | Recall orchestrator |
| Consolidation | `IncrementalRecent`, `ProjectNightly`, `ProjectionRebuild`, `ContradictionReview`, `ProcedureMining`, `FailureLearning`, `CrossProjectWeekly` | Consolidation engine |
| Epistemic Drive | `Disabled`, `ObserveOnly`, `KnowledgeCoverageRefresh`, `EpistemicDriveScan`, `LearningOpportunityReview`, `ApprovedLearningTask` | Epistemic Drive engine and learning orchestrator |
| Trust/write policy | `ObserveOnly`, `DraftOnly`, `AutoAcceptLowRisk`, `HumanReviewRequired`, `LockedApprovedRecords` | Memory governance |
| Compute placement | `InlineSmallBatch`, `BackgroundLocal`, `LocalIdleWorker`, `DistributedLanWorker` | Job coordinator |

## Mode Rules

- Modes must be explicit enum values or strongly typed options, not stringly typed ad hoc flags.
- Each mode must declare read authority, write authority, source scope, batch limit, retry behavior, and review behavior.
- The active mode must be persisted in source scan records, consolidation runs, projection runs, and recall traces.
- Epistemic Drive modes must persist source approval policy, proposal state, vector algorithm version, and evidence input hash.
- Changing modes must not reinterpret previous outputs without recording algorithm/profile version changes.

## Epistemic Drive By Operating Mode

### Local / Single-User Mode

- Run coverage refresh and proposal creation locally.
- Use local project sources, repositories, uploaded files, and approved local documentation.
- If internet is disabled, create proposals that ask for sources or probing rather than external study.
- Human approval can be the local user, but high-risk procedures still need explicit validation.

### Project Mode

- Scope coverage maps and proposals to the active project.
- Project graph and mindmap directions determine active project direction vectors.
- Recall traces, workflow failures, process runs, and user corrections remain project-scoped.
- Learning tasks update project memory first; cross-project promotion is separate.

### Cross-Project Memory Mode

- Aggregate repeated gaps across projects into reusable opportunities only after policy filtering.
- Do not leak project-private source text, source locators, or evidence details across project boundaries.
- Global proposals must use sources approved for global reuse.
- Maintain separate project coverage/confidence and global coverage/confidence.

### Distributed Idle Compute Mode

- Workers may compute embeddings, clusters, coverage projections, candidate gap evidence, and source-independent statistics.
- Workers cannot create authoritative proposals, approve learning tasks, write durable memory, or update Qdrant directly.
- Coordinator validates input hashes, output hashes, source scope, worker identity, algorithm version, and policy.

### Enterprise / Team Mode

- Learning proposals can be assigned to humans, teams, or approved agent workflows.
- Approval policy may require role-based signoff for security, deployment, compliance, customer, or legal knowledge.
- Audit records must show approver, scope, source trust, learning outputs, and promotion decisions.
- Team-level dashboards should show proposal age, high-risk pending items, and repeated cross-project gaps.

## High-Volume Data Strategy

Use partitioned, incremental processing:

- Partition source items by `ProjectId`, `SourceSystem`, `SourceKind`, `SourceKey`, and `ContentHash`.
- Track source cursors per adapter so scans can resume without full replay.
- Compute source hash, canonical hash, projection payload hash, and embedding profile hash separately.
- Use idempotency keys for source ingestion, projection upsert, review item creation, and distributed job output acceptance.
- Batch background jobs by item count, byte size, source type, and priority.
- Store large raw snapshots, context packs, reports, and worker outputs in storage/IPFS; keep relational rows as metadata and references.
- Keep recall interactive by querying bounded candidate sets and loading detailed source only after focus selection.

## EF Core Query Rules

Cognitive Memory will be read-heavy during recall, review, trace inspection, source scans, projection health checks, probing, and Epistemic Drive analysis. Every implementation subbundle that touches persistence must prove the following where applicable:

- Read-only queries use `AsNoTracking()`.
- Query handlers project to DTOs instead of materializing full entity graphs.
- Large lists use cursor/keyset pagination or stable bounded page tokens, not unbounded `ToListAsync()`.
- `Include` is avoided for review, trace, recall, source, and proposal screens unless the data shape is deliberately small; use explicit projections instead.
- If multiple child collections are ever loaded for detail views, use split-query strategy deliberately and document consistency tradeoffs.
- Repeated hot-path recall/projection queries become compiled-query candidates after the query shape stabilizes.
- Bulk state transitions such as activation decay, stale projection marking, supersession candidate expiration, and old worker lease expiry use set-based updates where audit policy allows it.
- No lazy loading proxies are allowed.
- Client-side evaluation warnings are treated as test failures for recall, consolidation, probing, and Epistemic Drive queries.

## Performance Guardrails

Use the common guardrail subbundle before implementation starts:

- Source scans, recall, consolidation, probing replay, projection rebuild, and Epistemic scans must have item-count, byte-count, elapsed-time, and cancellation budgets.
- Vector arrays are adapter boundary data. Hot projection and similarity code should avoid repeated `float[]` copies and should batch embeddings/search calls.
- Context-pack rendering must be allocation-conscious because it runs on interactive paths; store large packs in storage/IPFS and keep DB rows as metadata/reference records.
- JSON serialization must use source-generated contexts or cached serializer options for durable high-volume payloads.
- Qdrant outage, embedding provider outage, and source adapter failure must be explicit trace states, not silent lower-quality fallback.
- Performance proof must include the scan/checklist from the .NET performance review skill for any newly written hot-path code.

## Data Size Classes

| Class | Example | Expected handling |
|---|---|---|
| Tiny | One project mindmap under 500 nodes | Inline or short background jobs are acceptable. |
| Medium | Thousands of nodes, workflow events, and artifacts | Incremental source scans, batch projection, trace sampling. |
| Large | Many projects, repository files, emails, plugin streams, process history | Background queues, projection partitioning, source cursors, and strict limits. |
| Cross-project | Global topic and procedure promotion | Separate global projections and human review before promotion. |

## Recall Budgeting

Recall requests must include budgets:

- coarse candidate limit,
- graph expansion depth,
- vector result limit per collection,
- detail item limit,
- context token or character target,
- maximum source bytes to open,
- allowed source kinds,
- access policy context.

The trace must record which budget caused exclusion. Silent truncation is not acceptable.

## Concurrency And Idempotency

Use leases for:

- project source scans,
- project consolidation,
- projection rebuilds,
- distributed job claims,
- review item decision updates.

Every mutating operation must have a deterministic identity:

- source item key,
- content hash,
- canonicalization profile,
- projection profile,
- embedding profile,
- algorithm version,
- input hash.

## Failure Behavior

- Qdrant unavailable: continue lexical, graph, and source recall; mark projection channel unavailable in trace.
- Embedding provider unavailable: skip projection or use configured deterministic fallback only if the active mode permits it.
- Source adapter failure: record source scan failure with adapter, cursor, exception category, and retry eligibility.
- Consolidation failure: keep partial outputs in draft state and do not advance cursor until accepted writes are durable.
- Distributed worker failure: expire lease and requeue or mark failed based on retry policy.

## Operational Metrics

Track at minimum:

- source items scanned per adapter,
- changed source count,
- canonicalization throughput,
- projection queue depth,
- Qdrant upsert/search latency,
- recall latency by stage,
- knowledge coverage refresh duration,
- knowledge gaps by severity,
- learning proposals by category/state,
- proposal approval/snooze/rejection counts,
- approved learning task duration,
- probing-before-learning and probing-after-learning results,
- context-pack size,
- review queue age,
- consolidation run duration,
- worker claim/accept/reject counts.

## V1 Scale Target

V1 should prove correctness on project-scoped Workbench and process/workflow sources before optimizing for cross-project scale. The architecture must still include batching, cursors, hashes, and projection state from the first implementation because retrofitting them later would corrupt trust in existing memory records.

## Neuro-Cognitive Scale Rules

The neuro-cognitive records add several high-cardinality surfaces. Treat them as first-class scale concerns:

- evidence anchors can grow faster than memory items,
- claims can outnumber memory items,
- workspace slots and attention decisions can grow with active sessions,
- signals and prediction errors are event-like ledgers,
- episodes and replay jobs can grow with workflow/process/probe activity,
- answer-gate decisions can grow with recall/probe/MAF usage.

Required scale behavior:

- all list surfaces are paged or cursor-based,
- read screens use DTO projections and no-tracking queries,
- event ledgers are partition/filterable by project/time/kind,
- replay queues have bounded claim/lease behavior,
- workspace frames expire by default,
- detailed context packs, replay reports, and simulation outputs use storage references when large,
- signal/replay priority calculations preserve dimensions but may cache derived display scores.

Do not treat event-like ledgers as small lookup tables. Define indexes and retention/compaction policy before implementation starts.
