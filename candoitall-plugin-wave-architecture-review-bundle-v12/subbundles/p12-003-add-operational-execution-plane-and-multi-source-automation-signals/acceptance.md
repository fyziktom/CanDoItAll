# Acceptance

- Add explicit internal execution-envelope types separate from `ProjectObjectRecord`.
- Add a clear rule that messages/events/commands/wakeups do not become Workbench nodes by default.
- Replace singular automation signal consumption with a multi-source aggregation seam.
- Recommended exact types:
  - `IAutomationSignalSource`
  - `CompositeAutomationSignalProvider`
