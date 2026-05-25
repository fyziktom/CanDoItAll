# SB05 semantic invariants

## Invariant protected

The throughput claim must be backed by executable evidence against the real runtime paths.

## Producer/consumer lifecycle

The diagnostic test produces four automation envelopes and four connector commands, then consumes them through the normal dispatch/outbox services.

## Positive proof

The benchmark transcript records single-worker and four-worker timings for both automation dispatch and connector outbox processing.

## Adversarial negative proof

The test uses slow handlers so purely sequential execution is observable in the timing output.

## Anti-stub proof

The test verifies handler-side completion counts and runs through PostgreSQL-backed application services.
