# Target Solution

`ProcessRunAutomationDispatchService.ArtifactProjection.cs` should become an orchestration shell over module-local projection source coordinators.

## Module-Local Boundary

- Keep all new production code inside `src/CanDoItAll.Modules.Processes`.
- Keep public process contracts unchanged unless a current failing test forces a documented contract adjustment.
- Keep projection source vocabulary private or module-local in this bundle.

## Source Family Coordinators

| Source family | Target coordinator |
| --- | --- |
| Execution artifact | `ProcessExecutionArtifactProjectionCoordinator` |
| Process mock | `ProcessMockArtifactProjectionCoordinator` |
| Workspace-written | `ProcessWorkspaceWrittenArtifactProjectionCoordinator` |
| Existing managed | `ProcessExistingManagedArtifactProjectionCoordinator` |
| Response text | `ProcessResponseTextArtifactProjectionCoordinator` |
| Provider-native browser | `ProcessProviderNativeBrowserArtifactProjectionCoordinator` |
| Completed decision | `ProcessCompletedDecisionArtifactCoordinator` |

## Side-Effect Ownership

- Facts, snapshots, planners, and adapters remain side-effect-free.
- Readers own explicit content/file reads.
- Coordinators own side effects such as storage writes, DB writes, service scope usage, and `RecordArtifactAsync`.
- Candidate state mutation after write outcomes is centralized and covered by focused tests.

## Explicit Non-Goals

- No `CanDoItAll.Processes.Core`.
- No production process driver APIs.
- No UI changes.
- No browser/mobile proof artifacts.
