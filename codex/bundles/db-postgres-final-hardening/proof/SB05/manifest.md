# SB05 proof manifest

## Status

Completed with diagnostic benchmark evidence.

## Changed files

| File | SHA-256 before | SHA-256 after | Reason |
|---|---|---|---|
| `tests/CanDoItAll.Tests.Integration/AutomationRuntimeIntegrationTests.cs` | See `../SB08/transcripts/changed-file-hashes.txt` | See transcript | Add bounded parallelism diagnostic timing test. |
| `codex/bundles/db-postgres-final-hardening/proof/SB05/benchmark-report.md` | new | See transcript | Record numeric timing evidence. |

## Commands

| Command | Result | Transcript |
|---|---|---|
| `dotnet test ... --filter FullyQualifiedName~AutomationRuntimeIntegrationTests.Bounded_parallelism_diagnostic_records_connector_and_automation_timings` | Passed | `transcripts/bounded-parallelism-diagnostic.txt` |

## Source assertions

| Assertion | Source | Proof |
|---|---|---|
| Automation message dispatch uses configurable max parallelism. | `AutomationRuntimeIntegrationTests.cs`, runtime dispatcher source | Benchmark transcript and SB04 source audit. |
| Connector outbox processing uses configurable max parallelism. | `AutomationRuntimeIntegrationTests.cs`, connector outbox source | Benchmark transcript and SB04 source audit. |

## Negative tests

| Scenario | Expected | Result |
|---|---|---|
| Parallelism diagnostic executes the real PostgreSQL-backed handlers. | Four messages and four connector commands complete. | Passed. |

## Remaining risks

This is a diagnostic integration benchmark, not BenchmarkDotNet. It proves bounded parallel execution and captures timing deltas, but it is not a statistically rigorous throughput benchmark and does not include low-level SQL roundtrip counters.
