# SB06 - Throughput benchmark and runtime metrics

## Status

Completed.

## Objective

Add numeric proof that SQLite-era sequential bottlenecks are actually removed under PostgreSQL runtime.

## Covered inputs

- Previous benchmark report explicitly stated no numeric benchmark was captured.
- User asked to remove bottlenecks, not only introduce source-level parallelism.

## Exact source references

- `repo://codex/bundles/db-postgres-canonicality-and-throughput/proof/SB08/benchmark-report.md`
- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessOutbox.cs`
- `repo://src/CanDoItAll.Modules.Automation/Services/AutomationMessagingServices.cs`
- `repo://src/CanDoItAll.Modules.Workspace/Connectors/ConnectorOutboxService.cs`

## Deliverables

1. Add benchmark script/test harness with seeded PostgreSQL workload.
2. Measure:
   - records/sec,
   - average and p95 processing time,
   - claim batch size,
   - effective parallelism,
   - duplicate/stale finalization count.
3. Compare sequential mode vs bounded parallel mode.
4. Add runtime metrics counters/logging that can be observed in normal app diagnostics.

## Implementation steps

- Create deterministic seed data for process outbox, connector outbox, and automation delivery.
- Run with parallelism 1 and with configured parallelism.
- Capture outputs under `proof/SB06/benchmarks/`.
- Keep benchmark separate from normal unit tests if it is slow; provide explicit command.

## Do not do

- Do not present source audit as benchmark.
- Do not use only in-memory provider.
- Do not benchmark with an empty or tiny dataset.

## Acceptance checklist

- [x] Numeric benchmark exists.
- [x] Benchmark uses PostgreSQL.
- [x] Results show whether throughput improved.
- [x] Metrics include stale/duplicate protection counters.

## Proof required

- `proof/SB06/manifest.md`
- `proof/SB06/benchmark-report.md`
- `proof/SB06/benchmark-output.json`

## Browser validation logging

N/A.

## Progression gate

SB08 requires numeric benchmark or an explicit blocker.

## Suggested agent prompt

Implement SB06. Capture numeric PostgreSQL throughput proof for claimed process/automation/connector work, including sequential vs bounded parallel comparison.
