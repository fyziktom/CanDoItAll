# SB03 semantic invariants

## Invariant protected

Heartbeat or finalization lease loss must stop canonical mutation for the stale worker.

## Producer/consumer lifecycle

Renewal helpers produce refreshed lease ownership only when the existing token is still current. Processing monitors consume that boolean and halt finalization when renewal fails.

## Positive proof

Normal processing still completes under focused integration tests.

## Adversarial negative proof

Tests force a second worker to replace the token before the first worker resumes. The first worker records loss evidence and cannot write terminal state.

## Anti-stub proof

Assertions read persisted audit/telemetry rows and final row states after the race.
