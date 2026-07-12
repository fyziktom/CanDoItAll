# SB05 Semantic Invariants

## Canonical immutable usage facts

- Invariant ID: SB05-CANONICAL-FACT
- Source raw note: workflow analytics must report tokens/model/cost/time like processes while retaining one reasonable implementation across runtime/executor consumers.
- Expected behavior: every provider/executor usage activity is represented as an immutable typed observation with stable identity, exact token dimensions, pricing knowledge/provenance, timestamps, producer/node/executor identity, and launch origin.
- Disallowed shallow implementation: collapse observations into WorkflowUsageMetrics, synthesize random replay IDs, clamp corrupt token dimensions, equate unknown cost with zero, or discard failure observations.
- Failing-first test: four canonical mapping/flow/contract/pricing gates failed in bundle://proof/SB05/failing-first.txt.
- Passing proof: bundle://proof/SB05/passing-unit.txt proves one-to-one provider mapping, deterministic synthetic IDs, corrupt-dimension rejection, known-free/unknown separation, executor flow, and failure flow.
- Red-team negative case: negative/cached-over-input/inconsistent totals, invalid pricing combinations, and null-run store appends throw explicit exceptions.
- Compatibility: WorkflowUsageMetrics remains a projection for existing clients; it is not the authoritative stored fact.

## Correlate before persistence and never orphan telemetry

- Invariant ID: SB05-CORRELATION-PERSISTENCE
- Expected behavior: pre-correlation execution facts may have nullable RunId, but both real append paths correlate before persistence; stores reject null RunId and PostgreSQL enforces NOT NULL.
- Disallowed shallow implementation: persist a nullable/orphan row that analytics cannot select, infer process origin from legacy caller GUIDs, or parse OriginJson for the indexed process query.
- Passing proof: in-memory, EF-in-memory, and real PostgreSQL tests in bundle://proof/SB05/passing-unit.txt and bundle://proof/SB05/passing-persistence.txt.
- Production assertions: typed ProcessRun/Assignment wrappers project to separately indexed OriginProcessRunId/OriginProcessAssignmentId columns; OriginJson remains authoritative serialized origin.
- Red-team negative case: WorkflowUsageObservationCorrelationException is thrown before any write; a deliberately invalid store also proves the process rollup rejects an uncorrelated fact defensively.

## Idempotent raw facts and complete aggregates

- Invariant ID: SB05-IDEMPOTENT-AGGREGATION
- Expected behavior: equal stable IDs are idempotent, different immutable facts with the same ID are corruption, totals use every filtered persisted run, and RecentTake bounds only recent presentation rows.
- Disallowed shallow implementation: list-all raw rows as the persistent aggregate contract, sum only the first eight runs, overwrite same-ID drift, or count agent/workflow telemetry twice in a process rollup.
- Passing proof: 22 focused tests and one real PostgreSQL test in bundle://proof/SB05/passing-unit.txt and bundle://proof/SB05/passing-persistence.txt.
- Production assertions: IWorkflowUsageAnalyticsStore is the aggregate seam; the persistent implementation groups in the database; raw and aggregate services resolve to the same scoped persistent instance.
- Red-team negative case: atomic batch conflict, progress/result duplicate, concurrent-style idempotent retry, complete process-record drift, and one-count process merge are named in passing proof.

## Explicit time, pricing, and API validation

- Invariant ID: SB05-EXPLICIT-ANALYTICS
- Expected behavior: terminal duration uses TerminalAtUtc, active duration uses injected TimeProvider, unknown historical terminal duration remains unavailable, known-free cost is distinct from unknown cost, and invalid explicit API limits return 400.
- Disallowed shallow implementation: infer terminal time from UpdatedAtUtc, use DateTimeOffset.UtcNow directly, report unknown price as free, parse event payload JSON, silently clamp explicit input, or use page-sized rows for totals.
- Passing proof: bundle://proof/SB05/passing-unit.txt, bundle://proof/SB05/passing-api.txt, and bundle://proof/SB05/migration-script.txt.
- Red-team negative case: terminal run without TerminalAtUtc, explicit take 0/501, unknown pricing, and failed backend usage are all tested.
- Downstream dependency check: SB06 consumes typed WorkflowAnalyticsSnapshot only; browser presentation remains SB06/SB07 and is N/A for SB05.

## Evidence Contract

- Passing test: canonical mapping, correlation, immutable persistence, database aggregation, process rollup, API validation, and duration/pricing tests are recorded in `bundle://proof/SB05/passing-unit.txt`, `bundle://proof/SB05/passing-persistence.txt`, and `bundle://proof/SB05/passing-api.txt`; validator index: `bundle://proof/SB05/transcripts/closure.txt`.
- Changed source files: the complete production/test hash table is in `bundle://proof/SB05/manifest.md`, including `repo://src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.Core/WorkflowAnalyticsQueryService.cs` at `e7b84dc7bb4129e0e869b2bb6e781b9338bfa5dec49491514c7628f244eda869` and `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Persistence/PersistentWorkflowUsageObservationStore.cs` at `d7998fe182944f79ce3302a62ffd796c61c0f8781c54d16388bf0bdb10900e19`.
