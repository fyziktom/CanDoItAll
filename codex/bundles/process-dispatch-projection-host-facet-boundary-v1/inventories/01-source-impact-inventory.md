# Source impact inventory

Primary current files:

| File | Current role | Concern |
| --- | --- | --- |
| `src/CanDoItAll.Modules.Processes/Automation/Dispatch/IProcessArtifactProjectionHost.cs` | broad projection host adapter interface | too many responsibilities, many dispatcher nested aliases |
| `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjectionHost.cs` | dispatcher adapter implementing broad host | large forwarding surface back into dispatch service |
| `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionOrchestrator.cs` | source-family order orchestrator | good seam, but currently creates coordinators using broad host |
| `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionContext.cs` | per-run projection context | still carries dispatcher nested types; acceptable temporarily |
| `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessExecutionArtifactProjectionCoordinator.cs` | execution artifact source family | currently depends on broad host |
| `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessMockArtifactProjectionCoordinator.cs` | process mock source family | should use process-mock/path/file facets |
| `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessWorkspaceWrittenArtifactProjectionCoordinator.cs` | workspace-written source family | should use workspace-write/path/matcher facets |
| `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessExistingManagedArtifactProjectionCoordinator.cs` | existing managed source family | should use existing-managed/path/file facets |
| `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessResponseTextArtifactProjectionCoordinator.cs` | response text source family | should use response/path/file facets |
| `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessProviderNativeBrowserArtifactProjectionCoordinator.cs` | provider-native browser source family | should use browser output/path/file facets |
| `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessCompletedDecisionArtifactCoordinator.cs` | completed-decision source family | should use decision/lineage/candidate-state facets |
| `tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs` | architecture guardrails | extend with host/facet assertions |
| `tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs` | projection/runtime integration proof | extend focused projection matrix if needed |
