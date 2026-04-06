# Acceptance

- Add hosted workers using the host/runtime infrastructure.
- Add a worker for due trigger dispatch.
- Add a worker for connector outbox pending commands.
- Add a worker for queued background jobs or replace the current queue with the durable message plane.
- Register the workers in startup/DI so they are active at runtime.
- The current `ProcessPendingAsync(...)` connector outbox path must be called automatically by a worker.
- The current in-memory-only background queue must be retired or reduced to a non-authoritative optimization layer.
