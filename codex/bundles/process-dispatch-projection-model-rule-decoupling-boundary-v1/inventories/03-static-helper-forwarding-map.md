# Static Helper Forwarding Map

The current facet implementations still call many `ProcessRunAutomationDispatchService.*` static helpers. Do not move everything blindly. Classify first:

| Helper family | Example calls | Target |
| --- | --- | --- |
| Path resolution | `TryResolveArtifactFullPath`, `ResolveScopedManagedRelativePath`, `IsWithinWorkspace` | `ProcessProjectionPathRules` / path resolver facet. |
| Artifact classification | `GuessContentTypeFromPath`, `ResolveStorageContentKind`, `ResolveProcessArtifactKind` | `ProcessProjectionArtifactClassificationRules`. |
| Expectation matching | `ResolveArtifactExpectation`, `MatchExpectedArtifactId`, `WorkspaceWrittenFileMatchesExpectedArtifact` | `ProcessProjectionExpectationMatchingRules`. |
| Process mock | `ResolveProcessMockArtifactProjections`, `ProcessMockArtifactMatchesExpectation` | `ProcessProjectionProcessMockRules`. |
| Response text | `ShouldProjectResponseTextArtifacts`, `ResolveProjectableResponseArtifactText` | `ProcessProjectionResponseTextRules`. |
| Browser output | `ResolveProviderNativeBrowserToolName`, `MatchesExpectedBrowserOutputFile` | `ProcessProjectionBrowserOutputRules`. |
| Completed decision | `BuildCompletedDecisionArtifact*`, `ResolveCompletedDecisionArtifactTrustStatus` | `ProcessProjectionDecisionArtifactRules`. |
| Lineage | `BuildArtifactProjectionLineage` | `ProcessProjectionLineageFactory`. |
