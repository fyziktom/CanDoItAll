# Forbidden patterns

- Do not rely on manual calls to drain pending work.
- Do not keep background jobs as tracked-only metadata with inline execution.
- Do not use `Task.Run(...)` in services/controllers as the worker substitute.
