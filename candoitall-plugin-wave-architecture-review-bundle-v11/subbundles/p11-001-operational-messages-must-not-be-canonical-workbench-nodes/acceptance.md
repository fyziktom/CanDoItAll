# Acceptance

- Introduce explicit internal execution-envelope types that are separate from `ProjectObjectRecord`.
- Add a clear rule and code path that messages/events/commands/wakeups do **not** become Workbench nodes by default.
- Introduce a multi-source signal aggregation seam.
- Replace singular automation signal consumption with a composite/aggregated shape.
- Recommended exact types:
  - `IAutomationSignalSource`
  - `CompositeAutomationSignalProvider`
- `AutomationWorkspaceService` (or equivalent) must aggregate multiple signal sources rather than consuming a single registration.
