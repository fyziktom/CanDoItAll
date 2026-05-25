# Proof manifest SB06

## Status

Completed.

## Owned requirements

- R6: PostgreSQL runtime throughput must have numeric evidence instead of source-only claims.
- R8: Runtime diagnostics must expose claim/finalization/idempotency protection counters.

## Changed files

| File | SHA-256 | Reason |
|---|---:|---|
| `repo://src/CanDoItAll.Infrastructure/Diagnostics/RuntimeClaimMetrics.cs` | `5D460D22065B2F3AE3763C66D30B0572FE5143A46B9E7E0D16740BB19BFFCA9F` | Adds runtime `System.Diagnostics.Metrics` instruments for claimed records, processed records, batch duration, stale finalizations, and duplicate suppressions. |
| `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessOutbox.cs` | `590F0A5C9EB47723A0403A72A8E41593B6EB9F6A486B0942C42B8F32125179E3` | Emits runtime claim metrics for process outbox PostgreSQL and fallback batches, stale finalization, and automation-dispatch duplicate suppression. |
| `repo://src/CanDoItAll.Modules.Automation/Services/AutomationMessagingServices.cs` | `A91578D23298CDD94DD99FA4B55E7D128B9FAC9FA2EAF35F2AC4DC3180CFE3FD` | Emits runtime claim metrics for automation delivery batches, stale finalization, and duplicate envelope suppression. |
| `repo://src/CanDoItAll.Modules.Workspace/Connectors/ConnectorOutboxService.cs` | `DF85A1ADE2AF49EA6DD13FC44AA2544BC4185C625EE7B385BBBC248674F9B248` | Emits runtime claim metrics for connector command batches, stale finalization, and duplicate command suppression. |
| `repo://tests/CanDoItAll.Tests.Integration/PostgreSqlRuntimeThroughputBenchmarkTests.cs` | `99AFCE2A484F55907C605F18232CFCBAA93E5660DC7E793ABBC4744C1E4C18E9` | Adds opt-in PostgreSQL throughput benchmark with deterministic seeded hot queues and sequential vs bounded-parallel comparison. |
| `bundle://proof/SB06/benchmark-output.json` | `2E12268CFD08D3AB12DDC93494A4A5641101891A3815DF9DB3D20F2440E75955` | Machine-readable benchmark result. |
| `bundle://proof/SB06/benchmark-report.md` | `E012A4F6640F8201B25AEF89F30025A8EF6A97E5143E4BBEB6D5B126E6C196D8` | Human-readable benchmark summary and comparison. |
| `bundle://proof/SB06/runtime-metrics-source-audit.log` | `D6B5C48B179FB9919333A57864D6F022BE387D24430F728CA628F41594681EC6` | Source audit for metrics and benchmark entry points. |

## Validation commands

| Command | Result | Transcript |
|---|---|---|
| Focused SB06 PostgreSQL throughput benchmark with `CANDOITALL_RUN_SB06_BENCHMARK=1` and `CANDOITALL_SB06_BENCHMARK_OUTPUT=bundle://proof/SB06/benchmark-output.json` | Passed, 1 test, numeric benchmark emitted | `bundle://proof/SB06/benchmark-run.log` |
| Runtime metrics and benchmark source audit with `rg` | Passed | `bundle://proof/SB06/runtime-metrics-source-audit.log` |

## Source assertions

- `ProcessOutboxService.ProcessPendingAsync` records claimed count, processed count, batch duration, requested batch size, and effective parallelism for both PostgreSQL and fallback claim paths.
- `AutomationMessageDispatcher.DispatchPendingAsync` records the same batch metrics for automation deliveries and records stale finalization when a claimed delivery can no longer be finalized.
- `ConnectorOutboxService.ProcessPendingAsync` records the same batch metrics for connector commands and records stale finalization when a claimed command loses canonical ownership.
- Duplicate suppression counters are emitted by process automation-dispatch dedupe, automation envelope dedupe, and connector command idempotency dedupe paths.
- The opt-in benchmark uses migrated PostgreSQL schema, deterministic seeded rows, and `FOR UPDATE SKIP LOCKED` claim SQL for process outbox, automation delivery, and connector command workloads.

## Semantic adequacy

The previous benchmark artifact admitted that no numeric before/after benchmark had been captured. SB06 closes that gap with `bundle://proof/SB06/benchmark-output.json`:

- process outbox improved from 64.264 records/sec to 419.581 records/sec at effective parallelism 8
- automation delivery improved from 64.359 records/sec to 434.896 records/sec at effective parallelism 8
- connector command improved from 64.307 records/sec to 411.112 records/sec at effective parallelism 8
- all six workload/mode runs processed 768/768 seeded records
- stale finalization probes were rejected in every workload/mode
- duplicate suppression was observed through a real automation publisher dedupe-key probe

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
|---|---|---|---|---|
| Runtime claim metrics meter | `repo://src/CanDoItAll.Infrastructure/Diagnostics/RuntimeClaimMetrics.cs` | outbox/dispatcher services listed in `bundle://proof/SB06/runtime-metrics-source-audit.log` | `bundle://proof/SB06/benchmark-output.json` observed all metric instruments | Previous benchmark report had no numeric runtime diagnostics |
| Process outbox throughput | `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessOutbox.cs` | `repo://tests/CanDoItAll.Tests.Integration/PostgreSqlRuntimeThroughputBenchmarkTests.cs` | `bundle://proof/SB06/benchmark-report.md` | Sequential baseline in the same run |
| Automation delivery throughput | `repo://src/CanDoItAll.Modules.Automation/Services/AutomationMessagingServices.cs` | `repo://tests/CanDoItAll.Tests.Integration/PostgreSqlRuntimeThroughputBenchmarkTests.cs` | `bundle://proof/SB06/benchmark-report.md` | Sequential baseline in the same run |
| Connector command throughput | `repo://src/CanDoItAll.Modules.Workspace/Connectors/ConnectorOutboxService.cs` | `repo://tests/CanDoItAll.Tests.Integration/PostgreSqlRuntimeThroughputBenchmarkTests.cs` | `bundle://proof/SB06/benchmark-report.md` | Sequential baseline in the same run |

## Residual risks

The benchmark includes a deterministic 2 ms simulated side-effect delay so concurrency benefits are measurable without invoking external systems. It proves database claim/finalization throughput and runtime diagnostic emission, not end-to-end external connector/provider latency.
