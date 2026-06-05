# Test Impact Inventory

Expected affected slices:

- `ProcessRunAutomationDispatchServiceTests` concurrency/stale/recovery cases.
- Process filtered integration tests for automation dispatch.
- Unit architecture tests in `ProcessAgentExecutionBoundaryArchitectureTests`.
- Existing provider/tool/artifact/finalizer tests as regression smoke.
- No Playwright test unless UI files unexpectedly change.

Suggested filters:

- `FullyQualifiedName=CanDoItAll.Tests.Unit.ProcessAgentExecutionBoundaryArchitectureTests.Process_core_and_driver_pack_projects_are_not_introduced_prematurely`
- `FullyQualifiedName=CanDoItAll.Tests.Unit.ProcessAgentExecutionBoundaryArchitectureTests.Bundle_proof_paths_do_not_contain_mobile_or_small_screen_artifacts`
- `FullyQualifiedName~CanDoItAll.Tests.Integration.ProcessRunAutomationDispatchServiceTests&FullyQualifiedName~BlockingAutomationExecutionRun`
- `FullyQualifiedName~CanDoItAll.Tests.Integration.ProcessRunAutomationDispatchServiceTests&FullyQualifiedName~RecoverableAutomationExecutionRun`
- `FullyQualifiedName~CanDoItAll.Tests.Integration.ProcessRunAutomationDispatchServiceTests&FullyQualifiedName~FreshAutomationDispatch`
- `FullyQualifiedName~CanDoItAll.Tests.Integration.ProcessRunAutomationDispatchServiceTests&FullyQualifiedName~ConcurrentAutomation`
- `FullyQualifiedName~CanDoItAll.Tests.Integration.ProcessRunAutomationDispatchServiceTests&FullyQualifiedName~Subprocess`
- `FullyQualifiedName~CanDoItAll.Tests.Integration.ProcessRunAutomationDispatchServiceTests&FullyQualifiedName~Workflow`

Known baseline caveat:

- The full `ProcessAgentExecutionBoundaryArchitectureTests` class currently includes historical bundle artifact assertions for bundles not present in this checkout. Use current-scope guardrail tests unless this bundle explicitly restores or rewrites those historical bundle assertions.
