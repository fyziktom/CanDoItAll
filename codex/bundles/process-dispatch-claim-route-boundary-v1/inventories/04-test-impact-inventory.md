# Test Impact Inventory

Codex must update exact test names before movement.

Expected affected slices:

- `ProcessRunAutomationDispatchServiceTests` concurrency/stale/recovery cases.
- Process filtered integration tests for automation dispatch.
- Unit architecture tests in `ProcessAgentExecutionBoundaryArchitectureTests`.
- Existing provider/tool/artifact/finalizer tests as regression smoke.
- No Playwright test unless UI files unexpectedly change.

Suggested filters:

- `FullyQualifiedName~ProcessRunAutomationDispatchServiceTests&FullyQualifiedName~Dispatch`
- `FullyQualifiedName~ProcessRunAutomationDispatchServiceTests&FullyQualifiedName~Concurrency`
- `FullyQualifiedName~ProcessRunAutomationDispatchServiceTests&FullyQualifiedName~Recovery`
- `FullyQualifiedName~ProcessAgentExecutionBoundaryArchitectureTests`
