# SB05 benchmark report

## Scope

This report records an integration-level diagnostic benchmark for bounded parallelism. It is not a BenchmarkDotNet microbenchmark.

## Command

`dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-build --filter "FullyQualifiedName~AutomationRuntimeIntegrationTests.Bounded_parallelism_diagnostic_records_connector_and_automation_timings" --logger "console;verbosity=detailed"`

Transcript: `transcripts/bounded-parallelism-diagnostic.txt`

## Results

| Path | Single worker | Four workers | Observation |
|---|---:|---:|---|
| Automation dispatch | 746 ms | 205 ms | Parallel dispatch reduces elapsed time for slow handlers. |
| Connector outbox | 534 ms | 495 ms | Connector path executes successfully with bounded parallelism; handler/test overhead dominates this small sample. |

## Interpretation

The automation dispatch result demonstrates the intended throughput effect clearly. Connector outbox now accepts and uses bounded max parallelism, but this small four-command diagnostic is not sufficient to claim a large connector throughput gain.

## Caveats

No low-level SQL roundtrip counter was added. The proof is numeric timing plus source-level verification of batched claiming and bounded parallel processing.
