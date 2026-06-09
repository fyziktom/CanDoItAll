# SB007 Outbox Dispatch Inventory

## Status
Completed.

## Source-Backed Findings
- Run start enqueues durable automation dispatch records through `ProcessOutboxService.EnqueueAutomationDispatchAsync`.
- `ProcessOutboxRecord` carries typed status, attempt count, lease token, lease expiry, retry timing, and process/run correlation fields.
- `ProcessOutboxService.ProcessPendingAsync` selects due pending records, claims them before dispatch, and processes claimed batches.
- PostgreSQL uses an atomic claim query with `FOR UPDATE SKIP LOCKED`; non-PostgreSQL paths still claim with a guarded `ExecuteUpdateAsync`.
- `ProcessOutboxDrainWorker` waits for startup recovery, drains pending records in a scoped service provider, and uses `ProcessRuntimeOptions` for batch size and parallelism.
- Hosted worker registration is gated by runtime lane policy in `ProcessesModuleServiceCollectionExtensions`.

## Proof
- Source inventory: `bundle://proof/SB007/transcripts/outbox-dispatch-source-inventory.txt`
- No transient bundle-path scan: `bundle://proof/SB007/transcripts/no-transient-bundle-path-scan.txt`
- Anti-stub/runtime-host drift scan: `bundle://proof/SB007/transcripts/anti-stub-and-runtime-host-drift-scan.txt`

## Downstream Gate
SB008 owns deterministic drain/claim tests. SB009 owns critical hosted-worker readiness and stale-worker negative proof.
