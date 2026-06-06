# Current Dependency Surface

- Source-family coordinators depend on repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/IProcessArtifactProjectionHost.cs and `ProcessArtifactProjectionContext` instead of `ProcessRunAutomationDispatchService`.
- Dispatcher-private helper access is isolated behind repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjectionHost.cs.
- Candidate mutation is held in repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionCandidateState.cs and remains explicit in the projection context.
- Writes and record-only behavior still flow through repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionWriteCoordinator.cs and `ProcessArtifactProjectionRecordOnlyCoordinator`.
- No Process Core or production process-driver API exists in repo://src/CanDoItAll.Modules.Processes, proven by bundle://proof/shared/transcripts/source-scans.txt.
