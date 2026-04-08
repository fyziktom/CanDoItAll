# Acceptance

- Add hosted workers using `IHostedService` / `BackgroundService`.
- Add a worker for due trigger dispatch.
- Add a worker for connector outbox pending commands.
- Add a worker for queued background work or replace the current in-memory queue with the durable message plane.
- Register the workers in startup/DI so they are active at runtime.
