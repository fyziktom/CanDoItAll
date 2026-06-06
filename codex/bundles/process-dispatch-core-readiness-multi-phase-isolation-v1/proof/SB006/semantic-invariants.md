# SB006 Semantic Invariants

- Route order remains controlled by `ProcessDispatchRouteHandlerFactory` and `ProcessDispatchRouteOrderAssertion`.
- Handler classes do not take `ProcessRunAutomationDispatchService` directly.
- Dispatcher composition remains the only place where route services are assembled.
