# SB009 Semantic Invariants

## Status
Completed.

## Invariant SB009_INV_001
- Invariant ID: `SB009_INV_001`
- Source raw note: "Review real code, not only bundle report" and "Determine real test outcome."
- Expected behavior: Durable process automation dispatch is claimed before execution, cannot be processed twice by parallel drains, renews leases for long work, rejects stale-worker finalization after lease loss, and has a registered hosted drain worker in the local source-watch runtime lane.
- Disallowed shallow implementation: Treating enqueue as execution, dispatching inline from run start, ignoring lease ownership, or registering a worker in the wrong runtime lane.
- Failing-first test: `Automation_dispatch_stale_worker_cannot_finalize_after_lease_is_stolen` proves a stale worker cannot complete canonical state after lease theft.
- Passing test: Eight focused integration tests passed in `bundle://proof/SB009/transcripts/dispatch-claim-worker-integration-tests.txt`.
- Changed source files: No production source changed in SB009. Current source hashes are captured in `bundle://proof/SB009/manifest.md`.
- Production assertions: `bundle://proof/SB009/transcripts/dispatch-worker-source-assertions.txt` cites enqueue, claim, PostgreSQL claim, finalization, lease renewal, worker, and DI registration surfaces.
- Red-team negative case: `bundle://proof/SB009/red-team/stale-worker-finalization-rejection.txt` rejects stale-worker finalization.
- Downstream dependency check: SB010 route/finalizer tests may start because SB009 proves durable dispatch can actually be drained by service and hosted-worker paths.

## Shallow-Pass Trap
A fake Gate C closure could prove only that `ProcessOutboxRecord` exists or that `ProcessPendingAsync` returns a count. SB009 rejects that by requiring duplicate suppression, parallel claim exclusion, long-work lease renewal, stale-worker finalization rejection, runtime option defaults, and hosted-worker registration policy.

## Semantic Positive Proof
- `bundle://proof/SB009/transcripts/dispatch-claim-worker-integration-tests.txt`
- `bundle://proof/SB009/transcripts/dispatch-worker-source-assertions.txt`

## Adversarial Negative Proof
- `bundle://proof/SB009/red-team/stale-worker-finalization-rejection.txt`

## Anti-Stub Audit
- `bundle://proof/SB009/transcripts/anti-stub-and-runtime-host-drift-scan.txt`
- Matches are documentation and negative test assertions, not an execution-capable process-driver runtime host, process-driver registry, selector, or production `NotImplemented` path.

## Production Behavior Artifact Matrix
| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| Automation dispatch record | `ProcessOutboxService.EnqueueAutomationDispatchAsync` | Outbox drain service and hosted worker | Starts pending, records attempt count, and completes only after dispatch service returns | Duplicate pending dispatch reuse prevents duplicate work |
| Claim/lease | `TryClaimRecordAsync` and PostgreSQL `ClaimPendingRecordsPostgreSqlAsync` | `ProcessClaimedAsync` and `TryFinalizeClaimedRecordAsync` | Lease token gates attempt start, renewal, and finalization | Parallel drain and stale-worker tests reject double dispatch and stale completion |
| Hosted drain worker | `ProcessOutboxDrainWorker` registered by `AddProcessesModule` | Local source-watch runtime lane | Waits for startup recovery and drains batches with configured parallelism | Policy tests reject registration in published lanes and require registration in source-watch lane |
