# Documentation-Only Driver Readiness Map

This bundle prepares vocabulary for possible future driver extraction but does not implement production driver APIs.

| Future evidence concept | Current module-local projection family | Current source | Production API now? |
| --- | --- | --- | --- |
| `ExecutionArtifactEvidence` | Execution artifact projection | repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessExecutionArtifactProjectionCoordinator.cs | No |
| `ProcessMockEvidence` | Process mock artifact projection | repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessMockArtifactProjectionCoordinator.cs | No |
| `WorkspaceMutationEvidence` | Workspace-written artifact projection | repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessWorkspaceWrittenArtifactProjectionCoordinator.cs | No |
| `ExistingManagedArtifactEvidence` | Existing managed artifact projection | repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessExistingManagedArtifactProjectionCoordinator.cs | No |
| `ResponseTextEvidence` | Response-text artifact projection | repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessResponseTextArtifactProjectionCoordinator.cs | No |
| `ProviderNativeBrowserEvidence` | Provider-native browser projection | repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessProviderNativeBrowserArtifactProjectionCoordinator.cs | No |
| `CompletedDecisionEvidence` | Completed-decision record-only projection | repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessCompletedDecisionArtifactCoordinator.cs | No |
| `ArtifactProjectionHost` | Internal dependency surface | repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/IProcessArtifactProjectionHost.cs | Internal only |
| `ProjectionSourceCoordinator` | Internal source-family interface | repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/IProcessArtifactProjectionSourceCoordinator.cs | Internal only |

Guardrail proof in bundle://proof/shared/transcripts/source-scans.txt shows no `CanDoItAll.Processes.Core`, no `IProcessDriverPack`, no `IProcessDriverRegistry`, no `ProcessDriverRegistry`, and no `ProcessDriver` production API in repo://src/CanDoItAll.Modules.Processes.
