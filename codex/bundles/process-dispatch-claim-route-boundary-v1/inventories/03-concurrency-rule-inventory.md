# Concurrency Rule Inventory

Codex must update this with exact methods and parity tests.

Initial candidate methods:

| Method | Current file | Move target |
| --- | --- | --- |
| `HasBlockingAutomationExecutionRun` | `Concurrency.cs` | selection helper |
| `ResolveBlockingAutomationExecutionRunId` | `Concurrency.cs` | selection helper |
| `ResolveRecoverableAutomationExecutionRunId` | `Concurrency.cs` | selection helper |
| `ResolveCompetingActiveAutomationExecutionAsync` | `Concurrency.cs` | keep async query; delegate pure selection |
| `ShouldSkipAutomationCompletionTransition` | `Concurrency.cs` | selection helper or transition guard helper |
| `IsConcurrentAutomationSessionBusyException` | `Concurrency.cs` | selection/helper |
| `ShouldSkipFreshAutomationDispatch` | `Concurrency.cs` | route planner |
| `IsStaleAutomationExecutionRun` | `Concurrency.cs` | selection helper |
| `IsRecoverableExecutionRunForCurrentAttempt` | `Concurrency.cs` | selection helper |
