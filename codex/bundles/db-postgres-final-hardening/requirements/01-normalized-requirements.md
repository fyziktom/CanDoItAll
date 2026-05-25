# Normalized requirements

## R1: Review closure

Document what the latest Codex iteration fulfilled, what remains open, and why.

## R2: Final leased-work canonicality

Any worker that claims a DB row must only write final canonical state while it still owns the lease.

## R3: Lease-loss behavior

Losing a lease must be a state-changing stop condition, not only a warning.

## R4: Throughput unlock

PostgreSQL batch claim paths must use bounded parallelism with safe partitioning and proven non-duplication.

## R5: Benchmark proof

Add numeric benchmark/profiling evidence for:
- single worker vs bounded parallel,
- query count/round trips,
- duplicate execution prevention.

## R6: Runtime DB source of truth

The running process has exactly one canonical runtime DB profile. Persisted activation is pending-restart state until process restart.

## R7: Profile-specific maintenance boundary

Profile-specific contexts are allowed only for schema checks, bootstrap, transfer, and explicit maintenance; not normal runtime request/worker hot paths.

## R8: Final validation closure

Broad tests or a documented quarantine/credential plan must be part of merge readiness.
