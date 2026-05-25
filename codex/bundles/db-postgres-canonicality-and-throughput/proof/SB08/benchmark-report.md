# Benchmark report

## Result

No numeric before/after wall-clock benchmark was captured for this bundle. The throughput closure uses deterministic concurrency proof instead.

## Deterministic proof

- Automation delivery dispatch claims a batch with `FOR UPDATE SKIP LOCKED`, returns `EnvelopeId`, groups by envelope, and processes envelope groups with bounded `Parallel.ForEachAsync`.
- Process outbox claims include `ProcessRunId` and `CommandKey`; claimed records are partitioned by process run and command key before bounded parallel processing.
- Connector outbox claims include `ProjectId`, `ConnectorPluginKey`, and `CommandKey`; claimed records are partitioned before bounded parallel processing.
- Defaults are conservative but greater than one where safe: automation message dispatch `4`, connector outbox `4`, process outbox uses default worker concurrency.
- Focused integration proof passed: `bundle://proof/SB08/transcripts/focused-integration-tests.txt`.
- Source audit proof passed: `bundle://proof/SB05/transcripts/bounded-parallelism-source-audit.txt`.

## Residual measurement gap

This proves the SQLite-era sequential bottleneck was removed from the code path, but it does not quantify runtime throughput improvement under production load. A follow-up benchmark should run with a seeded PostgreSQL workload and compare records/second before and after this branch.
