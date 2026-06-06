# Projection Host Facet Boundary Source Assertions

- Invariant ID: `SB72-INV-001`
- Source assertion: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionFacets.cs` defines small module-local projection facets and `ProcessArtifactProjectionFacetSet`.
- Source assertion: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjectionServices.cs` contains the only dispatcher-backed projection services implementation and implements the facet interfaces.
- Source assertion: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionOrchestrator.cs` wires source coordinators in the required order: execution, process mock, workspace-written, existing managed, response text, provider-native browser, completed decision.
- Source assertion: source-family coordinators no longer reference `IProcessArtifactProjectionHost`, `DispatcherArtifactProjectionHost`, or `ProcessRunAutomationDispatchService dispatchService`.
- Source assertion: candidate mutation remains centralized through `IProcessProjectionCandidateStateUpdater`, implemented by the nested services class delegating to `ProcessArtifactProjectionCandidateState`.
- Source assertion: file read/write/copy side effects in projection coordinators are routed through `IProcessProjectionFileIo`.
