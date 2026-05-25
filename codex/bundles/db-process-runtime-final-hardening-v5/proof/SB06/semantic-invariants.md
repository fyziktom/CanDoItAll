# Semantic invariants SB06

## Invariants to prove

- No stale worker may write canonical runtime DB state.
- Lease ownership must be explicit and verifiable.
- Retry behavior must be idempotent.
- PostgreSQL runtime must remain canonical.
- Throughput claims must be backed by numeric PostgreSQL proof.
- Runtime diagnostics must expose stale and duplicate protection counters.

## Negative proof

- `repo://codex/bundles/db-postgres-canonicality-and-throughput/proof/SB08/benchmark-report.md` explicitly stated that no numeric benchmark was captured.
- Source-level `FOR UPDATE SKIP LOCKED` and bounded parallel processing alone did not prove that the migrated PostgreSQL runtime removed the SQLite-era sequential bottleneck.
- Stale finalization and duplicate suppression were protected in code paths, but they were not exposed as normal runtime diagnostic counters before SB06.

## Positive proof

- `bundle://proof/SB06/benchmark-output.json` proves PostgreSQL benchmark execution with 768 seeded records per workload, claim batch size 64, sequential parallelism 1, and bounded parallelism 8.
- `bundle://proof/SB06/benchmark-report.md` shows throughput gains of 6.39x to 6.76x across process outbox, automation delivery, and connector command workloads.
- Every workload/mode processed all 768 seeded records and rejected one stale finalization probe.
- The duplicate suppression counter was observed through a real `IAutomationMessagePublisher` duplicate dedupe-key probe.
- `bundle://proof/SB06/runtime-metrics-source-audit.log` maps runtime metric emission to process, automation, and connector production paths.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
|---|---|---|---|---|
| `candoitall.runtime.claimed_records` | `repo://src/CanDoItAll.Infrastructure/Diagnostics/RuntimeClaimMetrics.cs` | process/automation/connector runtime services | `bundle://proof/SB06/benchmark-output.json` observed the instrument | No runtime metric before SB06 |
| `candoitall.runtime.processed_records` | `repo://src/CanDoItAll.Infrastructure/Diagnostics/RuntimeClaimMetrics.cs` | process/automation/connector runtime services | `bundle://proof/SB06/benchmark-output.json` observed the instrument | No runtime metric before SB06 |
| `candoitall.runtime.batch_duration` | `repo://src/CanDoItAll.Infrastructure/Diagnostics/RuntimeClaimMetrics.cs` | process/automation/connector runtime services | `bundle://proof/SB06/benchmark-output.json` observed the instrument | No runtime metric before SB06 |
| `candoitall.runtime.stale_finalizations` | `repo://src/CanDoItAll.Infrastructure/Diagnostics/RuntimeClaimMetrics.cs` | stale finalization paths in process/automation/connector services | `bundle://proof/SB06/benchmark-output.json` counted 6 stale probes | No runtime metric before SB06 |
| `candoitall.runtime.duplicate_suppressions` | `repo://src/CanDoItAll.Infrastructure/Diagnostics/RuntimeClaimMetrics.cs` | idempotency/dedupe paths in process/automation/connector services | `bundle://proof/SB06/benchmark-output.json` counted 1 duplicate suppression probe | No runtime metric before SB06 |
| PostgreSQL runtime throughput benchmark | `repo://tests/CanDoItAll.Tests.Integration/PostgreSqlRuntimeThroughputBenchmarkTests.cs` | SB06 proof report | `bundle://proof/SB06/benchmark-run.log` passed | Previous benchmark report had no numeric output |
