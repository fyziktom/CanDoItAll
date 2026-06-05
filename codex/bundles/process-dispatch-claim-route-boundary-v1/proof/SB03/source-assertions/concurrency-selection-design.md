# SB03 Source Assertions

- `bundle://inventories/03-concurrency-rule-inventory.md` maps current `Concurrency.cs` methods to pure helper candidates, route/transition helper candidates, and async adapter responsibilities.
- The design keeps `executionClient.ListExecutionRunsAsync`, `GetExecutionRunDetailAsync`, polling delays, and `ConcurrentAutomationExecution` construction outside pure helpers.
- Existing wrappers on `ProcessRunAutomationDispatchService` are intentionally preserved until parity tests prove downstream callers.
- The future helper cutline includes stale-run, active-run, current-attempt, recoverable-run, competing-run, fresh-recovery, completion-transition, and busy-exception semantics.
