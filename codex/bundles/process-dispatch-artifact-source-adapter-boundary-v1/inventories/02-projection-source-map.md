# Projection Source Map

Codex must update this table in SB02 with exact current line anchors before production movement.

| Projection source | Current method/region | External key builder | Side effects | Planned migration |
| --- | --- | --- | --- | --- |
| Agent execution artifact | `ProjectExecutionArtifactsAsync` | `BuildExecutionArtifactExternalReferenceKey` | file read, storage placement, artifact record | write coordinator migration in SB10 |
| Process mock artifact | `ProjectProcessMockArtifactsAsync` | `BuildProcessMockArtifactExternalReferenceKey` | file read, storage placement, artifact record | adapter in SB05 |
| Workspace-written artifact | `ProjectWorkspaceWrittenArtifactsAsync` | `BuildWorkspaceWrittenArtifactExternalReferenceKey` | file read, storage placement, artifact record | adapter in SB06 |
| Existing managed file | `ProjectExistingManagedArtifactFilesAsync` | `BuildExistingManagedArtifactExternalReferenceKey` | file read, storage placement, artifact record | adapter in SB06 |
| Assistant response text | `ProjectResponseTextArtifactsAsync` | `BuildResponseTextArtifactExternalReferenceKey` | storage placement, artifact record | adapter in SB08 |
| Provider-native browser artifact | `ProjectProviderNativeBrowserArtifactsAsync` | `BuildProviderNativeBrowserArtifactExternalReferenceKey` | file read, storage placement, artifact record | adapter in SB08 |
| Decision artifacts | `EnsureDecisionArtifactsForCompletedStepAsync` | mixed | artifact record | inventory only; no migration in this bundle unless trivial and proven |
