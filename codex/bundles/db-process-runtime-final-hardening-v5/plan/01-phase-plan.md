# Phase plan

## Subbundle dependency map

```mermaid
flowchart TD
  SB01["SB01 Validation evidence and merge scope"]
  SB02["SB02 Startup recovery lease reclaim canonicality"]
  SB03["SB03 Long-running process dispatch heartbeat"]
  SB04["SB04 Process outbox idempotency and side-effect canonicality"]
  SB05["SB05 PostgreSQL process DB indexes and claim query plan"]
  SB06["SB06 Throughput benchmark and runtime metrics"]
  SB07["SB07 Process DB red-team tests"]
  SB08["SB08 Final merge readiness"]

  SB01 --> SB02
  SB02 --> SB03
  SB03 --> SB04
  SB04 --> SB05
  SB05 --> SB06
  SB02 --> SB07
  SB03 --> SB07
  SB04 --> SB07
  SB06 --> SB08
  SB07 --> SB08
```

## Critical subbundles

- SB02: protects canonical lease ownership during recovery.
- SB03: protects long-running process execution from claim expiry.
- SB04: prevents duplicate side effects after lease loss/retry.
- SB07: proves the concurrency fixes semantically.

## Phase gates

Do not run benchmark/merge gates until SB02-SB04 pass semantic red-team tests.

Do not merge if broad validation remains unexplained or if a process worker can mutate state after losing a lease.
