# SB02 semantic invariants

## Invariant protected

A worker that no longer owns a lease must not commit terminal canonical state or audit records for leased work.

## Producer/consumer lifecycle

Claim helpers produce lease tokens. External handlers consume the claim. Finalization helpers are the only producers of terminal state and must re-check ownership at commit time.

## Positive proof

Focused integration tests prove normal retry/completion still succeeds after a legitimate worker owns the lease.

## Adversarial negative proof

Lease-steal tests force worker A to execute handler code after worker B owns the row. Worker A cannot complete, dead-letter, retry, or duplicate audit state.

## Anti-stub proof

Tests inspect persisted delivery/outbox/audit rows after the race, not just returned booleans.
