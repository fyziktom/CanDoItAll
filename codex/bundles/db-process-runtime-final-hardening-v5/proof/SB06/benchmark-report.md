# SB06 PostgreSQL Runtime Throughput Benchmark

## Summary

The SB06 benchmark used PostgreSQL with deterministic seeded rows for process outbox, automation delivery, and connector command claims. Each workload processed 768 seeded records with a claim batch size of 64, then compared sequential processing (`parallelism=1`) against bounded parallel processing (`parallelism=8`).

The benchmark passed on 2026-05-25. Full machine-readable output is in `bundle://proof/SB06/benchmark-output.json`; the command transcript is in `bundle://proof/SB06/benchmark-run.log`.

## Results

| Workload | Mode | Processed | Records/sec | Avg ms | P95 ms | Avg claim batch | Effective parallelism | Stale finals | Duplicate suppressions |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| process-outbox | sequential | 768/768 | 64.264 | 15.469 | 17.051 | 64 | 1 | 1 | 0 |
| process-outbox | bounded-parallel | 768/768 | 419.581 | 13.240 | 18.469 | 59.077 | 8 | 1 | 0 |
| automation-delivery | sequential | 768/768 | 64.359 | 15.469 | 17.206 | 64 | 1 | 1 | 0 |
| automation-delivery | bounded-parallel | 768/768 | 434.896 | 13.384 | 17.079 | 64 | 8 | 1 | 0 |
| connector-command | sequential | 768/768 | 64.307 | 15.482 | 16.780 | 64 | 1 | 1 | 0 |
| connector-command | bounded-parallel | 768/768 | 411.112 | 14.403 | 17.194 | 64 | 8 | 1 | 0 |

## Throughput Comparison

| Workload | Sequential records/sec | Bounded records/sec | Throughput multiplier |
| --- | ---: | ---: | ---: |
| process-outbox | 64.264 | 419.581 | 6.53x |
| automation-delivery | 64.359 | 434.896 | 6.76x |
| connector-command | 64.307 | 411.112 | 6.39x |

## Runtime Metrics

The benchmark observed all runtime metric instruments from `RuntimeClaimMetrics`:

| Instrument | Purpose |
| --- | --- |
| `candoitall.runtime.claimed_records` | records claimed per workload batch |
| `candoitall.runtime.processed_records` | records finalized per workload batch |
| `candoitall.runtime.batch_duration` | batch duration in milliseconds |
| `candoitall.runtime.stale_finalizations` | finalization attempts rejected by stale lease/state |
| `candoitall.runtime.duplicate_suppressions` | idempotent duplicate suppression events |

Protection counters from the run:

| Counter | Count | Source |
| --- | ---: | --- |
| stale finalization probes | 6 | one stale finalization attempt per workload/mode |
| duplicate suppression probe | 1 | `IAutomationMessagePublisher` duplicate dedupe-key publish |

## Command

```powershell
$env:CANDOITALL_RUN_SB06_BENCHMARK='1'
$env:CANDOITALL_SB06_BENCHMARK_OUTPUT=(Join-Path (Get-Location) 'codex\bundles\db-process-runtime-final-hardening-v5\proof\SB06\benchmark-output.json')
dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~PostgreSqlRuntimeThroughputBenchmarkTests.Run_sb06_postgresql_throughput_benchmark_when_enabled" -v:minimal
```

## Notes

- The benchmark is opt-in through `CANDOITALL_RUN_SB06_BENCHMARK=1`, so normal integration test runs compile it without paying benchmark runtime.
- The run emitted existing EF Core 10.0.0/10.0.4 assembly conflict warnings from the repo; the focused benchmark test still passed.
- The benchmark uses real PostgreSQL schema migration/bootstrap and direct `FOR UPDATE SKIP LOCKED` claim/finalization SQL for the three hot runtime queues.
