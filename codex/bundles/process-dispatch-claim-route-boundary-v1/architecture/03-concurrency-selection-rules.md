# Concurrency Selection Rules

Move pure rules from `Concurrency.cs` into module-local helpers after Gate A.

Pure selection helper candidates:

- blocking execution run detection and latest blocking run selection,
- current-attempt blocking run selection,
- recoverable terminal execution run selection,
- stale execution run detection,
- current-attempt matching,
- competing active execution selection after the async adapter supplies execution runs,
- concurrent busy exception detection.

Route or transition helper candidates:

- fresh recovery skip decision,
- completion-transition skip decision.

Async adapter responsibilities that must remain outside pure helpers:

- `executionClient.ListExecutionRunsAsync`,
- `executionClient.GetExecutionRunDetailAsync`,
- non-terminal polling and delay,
- constructing `ConcurrentAutomationExecution`,
- dispatcher logging and lifecycle return decisions.

The helper extraction must preserve existing `ProcessRunAutomationDispatchService` wrapper methods until parity tests prove all callers and filters continue to work.
