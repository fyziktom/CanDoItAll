# Projection Source Family Inventory

| Source family | Current coordinator | Must preserve |
| --- | --- | --- |
| Execution artifacts | `ProcessExecutionArtifactProjectionCoordinator` | first source-family slot, transient skip, missing file skip, expectation matching, write outcome mutation |
| Process mock artifacts | `ProcessMockArtifactProjectionCoordinator` | required expectation matching, multi-match exception, scoped path resolution, exception behavior |
| Workspace-written artifacts | `ProcessWorkspaceWrittenArtifactProjectionCoordinator` | governed path priority, workspace write receipt fallback, synthetic artifact metadata |
| Existing managed artifacts | `ProcessExistingManagedArtifactProjectionCoordinator` | expected managed path resolution, existing file matching, response-target reuse |
| Response text artifacts | `ProcessResponseTextArtifactProjectionCoordinator` | usable response content checks, declared/fallback path, existing target handling, write behavior |
| Provider-native browser artifacts | `ProcessProviderNativeBrowserArtifactProjectionCoordinator` | expected path and discovered output handling, browser output file safety, file copy behavior |
| Completed decisions | `ProcessCompletedDecisionArtifactCoordinator` | record-only behavior, trust/sensitivity, external reference key, lineage |
