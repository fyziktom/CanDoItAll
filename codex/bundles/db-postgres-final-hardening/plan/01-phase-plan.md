# Phase plan

## Subbundle dependency map

```mermaid
flowchart TD
    SB01["SB01 Merge evidence and residue cleanup"]
    SB02["SB02 Conditional finalization for leased outbox work"]
    SB03["SB03 Lease-loss hardening and heartbeat contracts"]
    SB04["SB04 Throughput defaults and runtime tuning"]
    SB05["SB05 Benchmark and query-count proof"]
    SB06["SB06 Process dispatch claim-first deep proof"]
    SB07["SB07 Canonicality invariants and admin boundaries"]
    SB08["SB08 Final validation and merge readiness"]

    SB01 --> SB02
    SB02 --> SB03
    SB03 --> SB04
    SB04 --> SB05
    SB02 --> SB06
    SB06 --> SB05
    SB02 --> SB07
    SB03 --> SB07
    SB05 --> SB08
    SB07 --> SB08
```

## Critical subbundles

- SB02 is critical: stale finalization can corrupt canonical state.
- SB03 is critical: heartbeat loss semantics define whether claims are trustworthy.
- SB07 is critical: prevents future source-of-truth drift.

## Phase gates

- Do not tune defaults in SB04 until SB02/SB03 prove stale workers cannot commit.
- Do not accept benchmark proof in SB05 unless duplicate-execution negative tests pass.
- Do not merge until SB08 closes broad test caveats or records explicit quarantines with owners.
