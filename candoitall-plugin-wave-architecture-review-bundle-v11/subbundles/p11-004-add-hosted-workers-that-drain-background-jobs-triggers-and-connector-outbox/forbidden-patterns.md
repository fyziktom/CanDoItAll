# Forbidden patterns

- Do not rely on manual admin calls to drain pending work.
- Do not keep background jobs as tracked-only metadata with inline execution.
- Do not use `Task.Run(...)` in controllers/services as the worker substitute.
