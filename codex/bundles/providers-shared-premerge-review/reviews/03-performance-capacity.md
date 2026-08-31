# Performance and capacity disposition

Paired baseline and repaired workloads are retained in reviews/test-results/sb05-baseline.trx and sb05-after.trx, with extracted metrics in reviews/sb05-measurements.json. Both selections passed 10/10, including the actual-recorder late-retry case. Allocation reductions are workload-specific, not throughput guarantees.

The additional six-case run, sb05-08-final.trx, passed catalog 1/50/200 cases, both upgrade/preservation lanes and the existing concurrent history scenario. On 10,000 live and 5,000 expired rows, 24 concurrent captures and 20 searches completed while all expired metadata was removed and live attempts survived. Begin p95 was 10.8204 ms; completion p95 was 10.7098 ms. The catalog stamp plan at 200 publications took 0.182 ms locally. The earlier 10,000-orphan workload drained in about 255 ms; its indexed 500-row selection took about 0.267 ms. These are local measurements with process/database noise.

## Scheduling limit

The existing maintenance worker runs every 20 seconds with a 10-second total pass budget, 2 seconds per source, default batch 500 and a separate 100-item cap per source. The timer is not a promise that every pass completes its full batch.

| Declared arrival scenario | Idealized service cap | Implication before overhead |
| --- | --- | --- |
| Outbox 5 items/s | 25 items/s | 10,000 queued items drain in at least 500 seconds while arrivals continue |
| Outbox 20 items/s | 25 items/s | The same backlog needs at least 2,000 seconds; little failure/latency margin |
| Outbox 30 items/s | 25 items/s | Backlog grows at least 5 items/s; default cadence is unsuitable |
| One canonical source 1 item/s | 5 items/s | 10,000 backfill items need at least 2,500 seconds |
| One canonical source 6 items/s | 5 items/s | Backlog grows at least 1 item/s |

The full pass also performs recovery, retention and other sources. Real drain can be slower. A deployment requiring near-real-time indexing at higher rates must measure arrival/backlog/checkpoint age and choose a bounded scheduling change under an explicit freshness target. No production arrival rate or freshness SLO was supplied, so this repair does not increase poll frequency, create a new scheduler or claim a general capacity pass.

The cache deliberately retains two lightweight database queries per hit for identity/version/secret revocation. Constant allowlists are reused; dynamic tool names remain request-owned. The relay removes avoidable whole-body copies but buffered responses and JSON output remain proportional to body size, bounded by the existing relay limit. No unbounded buffering or broad cache TTL was introduced.
