# Performance experiment report

All blank measurements are **not measured**, not zero.

## Experimental identity

Workload IDs; hypothesis; baseline source and changed source; working tree; siblings; SDK/runtime; OS/CPU/RAM; database server version/configuration; sanitized topology and RTT measurement method; asset/dependency mode; payload/record counts; actors and concurrency; source/authority branch; warmup, samples and repetitions.

## Correctness prerequisite

Name the deterministic race/fault cases that passed on the measured source. A faster path with different guarantees is not a valid optimization comparison. Keep user/LLM/external waits out of the measured SQL critical section and report their separate end-to-end costs.

## Results

| Metric | Baseline | Changed | Delta | Samples / variation | Interpretation |
|---|---|---|---|---|---|
| End-to-end p50 / p95 / p99 | | | | | |
| Accepted throughput / conflict rate | | | | | |
| SQL roundtrips / rows / query time | | | | | |
| Local gate wait / hold | | | | | |
| Advisory/row wait / transaction time | | | | | |
| Connection-pool wait | | | | | |
| CPU / allocation / GC | | | | | |
| Payload / receipt / WAL bytes | | | | | |
| Projection lag / drain rate | | | | | |
| Replans / wasted model work | | | | | |
| UI draft memory / interaction latency | | | | | |

## Confounders and conclusions

State cold/warm differences, noise, topology changes, outliers and rejected trials. Do not report one observation as a measured speedup. Separate derived arithmetic from measured values. Report whether the candidate investigation threshold was crossed and what architectural cause the evidence supports.

## Decision

Accept / investigate / reject optimization, with invariant proofs retained. Describe the next bounded experiment or explicit latency trade-off. Do not disable preconditions, authority or durable result handling to improve a benchmark.
