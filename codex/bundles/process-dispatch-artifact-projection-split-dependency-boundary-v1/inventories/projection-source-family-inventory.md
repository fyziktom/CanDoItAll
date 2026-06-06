# Projection Source Family Inventory

| Order | Family | Top-level coordinator | Dependency shape |
| --- | --- | --- | --- |
| 1 | Existing managed helper | repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessExistingManagedArtifactProjectionCoordinator.cs | `IProcessArtifactProjectionHost` |
| 2 | Execution artifacts | repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessExecutionArtifactProjectionCoordinator.cs | `IProcessArtifactProjectionHost` |
| 3 | Process mock artifacts | repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessMockArtifactProjectionCoordinator.cs | `IProcessArtifactProjectionHost` |
| 4 | Workspace-written artifacts | repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessWorkspaceWrittenArtifactProjectionCoordinator.cs | `IProcessArtifactProjectionHost` |
| 5 | Response-text artifacts | repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessResponseTextArtifactProjectionCoordinator.cs | `IProcessArtifactProjectionHost` plus existing-managed coordinator reuse |
| 6 | Provider-native browser artifacts | repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessProviderNativeBrowserArtifactProjectionCoordinator.cs | `IProcessArtifactProjectionHost` |
| 7 | Completed-decision record-only artifacts | repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessCompletedDecisionArtifactCoordinator.cs | `IProcessArtifactProjectionHost` |

Source-family order proof is in bundle://proof/shared/transcripts/source-scans.txt.
