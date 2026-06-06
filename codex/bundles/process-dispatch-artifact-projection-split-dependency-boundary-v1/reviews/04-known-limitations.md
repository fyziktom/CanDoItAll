# Known Limitations

- `dotnet build CanDoItAll.slnx --no-restore` passes with existing CA1416 warnings in repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.DotnetRunCleanup.cs. These warnings are outside the artifact projection boundary and pre-existing for this bundle scope.
- The broad `ProcessAgentExecutionBoundaryArchitectureTests` diagnostic is non-gating for this bundle and is recorded at bundle://proof/shared/transcripts/architecture-class-diagnostic.txt. It currently has missing historical bundle fixture failures for prior bundles and one stale non-projection claim-route source assertion.
- The gate tests for this bundle are the focused projection architecture tests and focused projection integration tests, both passing in bundle://proof/shared/transcripts/unit-projection-architecture-tests.txt and bundle://proof/shared/transcripts/integration-projection-tests.txt.
