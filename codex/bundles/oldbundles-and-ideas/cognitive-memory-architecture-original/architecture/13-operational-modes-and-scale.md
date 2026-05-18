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
