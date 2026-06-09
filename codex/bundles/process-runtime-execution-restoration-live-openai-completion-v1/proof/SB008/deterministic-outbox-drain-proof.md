# SB008 Deterministic Outbox Drain Proof

## Status
Completed.

## Behavior Proven
- A started process run leaves automation dispatch in a durable pending outbox record until `ProcessPendingAsync` drains it.
- Duplicate automation dispatch enqueue for the same run, step, and trigger reuses the existing pending command.
- Parallel `ProcessPendingAsync` calls do not dispatch the same automation record twice.

## Test Proof
- Focused integration transcript: `bundle://proof/SB008/transcripts/deterministic-outbox-drain-tests.txt`
- TRX artifact: `bundle://proof/SB009/test-results/SB009-dispatch-claim-worker.trx`
- Included tests:
  - `ProcessOutboxIntegrationTests.StartRunAsync_enqueues_automation_dispatch_for_durable_processing`
  - `ProcessOutboxIntegrationTests.Duplicate_automation_dispatch_enqueue_reuses_existing_pending_command`
  - `ProcessOutboxIntegrationTests.Parallel_ProcessPendingAsync_calls_do_not_dispatch_same_automation_record_twice`

## Source And Guard Proof
- Source assertions: `bundle://proof/SB008/transcripts/deterministic-outbox-source-assertions.txt`
- No transient bundle-path scan: `bundle://proof/SB008/transcripts/no-transient-bundle-path-scan.txt`
- Anti-stub/runtime-host drift scan: `bundle://proof/SB008/transcripts/anti-stub-and-runtime-host-drift-scan.txt`
