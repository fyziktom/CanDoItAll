# Acceptance

- Make durable envelope publish deduplication atomic.
- Make plugin ingress deduplication atomic.
- Make connector outbox idempotency atomic.
- Concurrent duplicate requests must return the already-existing row instead of failing the operation.
