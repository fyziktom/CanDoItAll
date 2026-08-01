# PostgreSQL Runtime Canonicality

CanDoItAll runtime services use the canonical `AppDbContext` factory for normal work. Profile-specific factories are reserved for explicit maintenance flows such as bootstrap, schema validation, profile transfer, and restart-pending administration. A persisted active-profile change is not a live runtime switch; it is a pending activation that takes effect after restart.

Leased background work follows a claim-first contract:

- Workers claim due rows with a lease token and lease expiry before executing side effects.
- Final state is written with a guarded update that matches the row id, current lease token, and unexpired lease.
- If the guarded update affects zero rows, the worker records lease loss and does not write completion, retry, or dead-letter state.
- Attempt-start records may exist for claimed work, but completion audit/dead-letter artifacts are only created after guarded finalization succeeds.

PostgreSQL batch workers are bounded-parallel by default:

- Automation message delivery: batch size `20`, max parallelism `4`.
- Connector outbox: batch size `20`, max parallelism `4`.
- Process outbox: batch size `20`, max parallelism `2`.

Single-thread mode remains available by configuring the corresponding max parallelism value to `1`. Parallel execution is partitioned by the canonical aggregate that can be mutated by the work item: automation delivery batches group by envelope id, connector batches group by project/plugin/command partition, and process outbox batches group by process run plus command key. This preserves per-aggregate ordering while allowing unrelated aggregates to drain concurrently.
