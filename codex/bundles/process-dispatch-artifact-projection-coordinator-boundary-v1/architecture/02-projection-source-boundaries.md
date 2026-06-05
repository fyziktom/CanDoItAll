# Projection Source Boundaries

| Source family | Current behavior to preserve | New owner pattern |
| --- | --- | --- |
| Execution artifact | Skip transient, duplicate external key, resolve path, read file, match expectation, write storage-backed record | `ProcessExecutionArtifactProjectionCoordinator` |
| Process mock | Match single required expectation, scoped managed path, hard-fail ambiguous/missing mock file, enforce expectation id | `ProcessMockArtifactProjectionCoordinator` |
| Workspace-written | Use session/receipt writes, governed path first, content/path match second, skip unreadable files | `ProcessWorkspaceWrittenArtifactProjectionCoordinator` |
| Existing managed | Resolve expected managed paths, text-probe small files, duplicate guard, write as managed-workspace-file | `ProcessExistingManagedArtifactProjectionCoordinator` |
| Response text | Project response text to declared/fallback artifact path only when eligible | `ProcessResponseTextArtifactProjectionCoordinator` |
| Provider-native browser | Resolve browser output files, safe path, non-empty file proof, write as browser-native source | `ProcessProviderNativeBrowserArtifactProjectionCoordinator` |
| Completed decision | Record-only completed decision artifact, no storage placement | `ProcessCompletedDecisionArtifactCoordinator` |

## Candidate State Updates

Candidate update after write outcome must be centralized:

- `ExternalReferenceKeys.Add(...)`
- `RecordedArtifactExpectationIds.Add(...)`

Do not scatter these mutations across coordinators unless a helper is used consistently and tested.
