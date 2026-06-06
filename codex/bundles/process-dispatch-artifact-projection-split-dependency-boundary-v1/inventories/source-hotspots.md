# Source Hotspots

- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjection.cs is now a 43-line facade that creates the projection context and delegates to `ProcessArtifactProjectionOrchestrator`.
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionOrchestrator.cs owns source-family order.
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/IProcessArtifactProjectionHost.cs is the explicit internal dependency surface used by source-family coordinators.
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjectionHost.cs adapts the dispatcher to the host boundary; source-family coordinators do not take the dispatcher object.
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjectionUtilities.cs contains projection helper methods extracted out of the facade.
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjectionCoordinators.cs remains only as an empty compatibility partial shim.
- Line-count and source-shape proof: bundle://proof/shared/transcripts/source-scans.txt.
