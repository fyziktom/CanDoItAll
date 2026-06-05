# Concurrency Selection Rules

Move pure rules from `Concurrency.cs` into a module-local helper.

Candidate rules:

- blocking execution run detection,
- recoverable execution run selection,
- stale execution run detection,
- current-attempt matching,
- competing active execution selection,
- fresh recovery skip decision,
- concurrent busy exception detection.

Do not move `executionClient.ListExecutionRunsAsync` calls yet. Those are still adapter/service calls and should remain in the dispatcher or a local coordinator with explicit tests.
