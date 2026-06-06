# Current host method inventory

Final state after execution:

- Broad host contract removed: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/IProcessArtifactProjectionHost.cs`.
- Broad dispatcher adapter removed: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjectionHost.cs`.
- Replacement facet contract file: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionFacets.cs`.
- Replacement dispatcher-backed implementation file: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjectionServices.cs`.

Facet method groups:

| Facet | Method count | Primary consumers |
| --- | ---: | --- |
| `IProcessProjectionClaimGuard` | 1 | Execution artifact coordinator |
| `IProcessProjectionPathResolver` | 7 | Execution, process mock, workspace-written, existing-managed, response-text, provider-native browser coordinators |
| `IProcessProjectionFileIo` | 5 | All file-reading/writing/copying source coordinators |
| `IProcessProjectionArtifactClassifier` | 6 | Execution, process mock, workspace-written, existing-managed, response-text, provider-native browser coordinators |
| `IProcessProjectionExpectationMatcher` | 7 | Execution, workspace-written, existing-managed, response-text, provider-native browser, completed decision coordinators |
| `IProcessProjectionProcessMockRules` | 2 | Process mock coordinator |
| `IProcessProjectionProjectStructureMatcher` | 2 | Workspace-written coordinator |
| `IProcessProjectionSessionObservationSource` | 4 | Workspace-written and provider-native browser coordinators |
| `IProcessProjectionResponseTextRules` | 4 | Response-text coordinator |
| `IProcessProjectionBrowserOutputRules` | 5 | Provider-native browser coordinator |
| `IProcessProjectionDecisionArtifactRules` | 5 | Completed decision coordinator |
| `IProcessProjectionLineageFactory` | 1 | Completed decision coordinator |
| `IProcessProjectionCandidateStateUpdater` | 3 | All recording coordinators |

Proof:

- `bundle://proof/shared/source-assertions/projection-facet-boundary.md`
- `bundle://proof/shared/transcripts/source-scan-no-broad-host.txt`
