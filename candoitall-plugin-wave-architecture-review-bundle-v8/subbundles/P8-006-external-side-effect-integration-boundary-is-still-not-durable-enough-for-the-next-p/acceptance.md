# Acceptance

The subbundle is only complete when all of these are true:

- The old seam is removed, not merely wrapped.
- The new owner of truth is explicit.
- The required tests were added/updated.
- The closure proof below can be attached with concrete evidence.

## Closure proof
Connector side-effecting operations use durable intent/outbox records and an execution worker with idempotency keys. Integration tests cover retry and crash-resume behavior. No direct side-effecting connector calls remain in request/transaction flows.
