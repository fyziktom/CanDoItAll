# SB02 proof manifest

## Status

Completed.

## Owned requirements

Runtime canonical profile and pending-next-restart profile must be represented separately in control-plane models, API DTOs, dev endpoints, and UI labels.

## Semantic invariant contract

`bundle://proof/SB02/semantic-invariants.md`

## Changed files

- `repo://src/CanDoItAll.Infrastructure/ControlPlane/DatabaseProfileModels.cs`
- `repo://src/CanDoItAll.Infrastructure/ControlPlane/DatabaseSwitchingAbstractions.cs`
- `repo://src/CanDoItAll.Modules.Workspace/Database/DatabaseProfileWorkspaceService.cs`
- `repo://src/CanDoItAll.Modules.Workspace/Pages/Components/DatabaseSourcesSettingsPanel.razor`
- `repo://src/CanDoItAll.Web/Api/CognitiveMemoryApi.DatabaseEndpoints.cs`
- `repo://src/CanDoItAll.Web/Api/CognitiveMemoryApiDtos.cs`
- `repo://src/CanDoItAll.Web/Components/Layout/MainLayout.DatabaseProfiles.cs`
- `repo://src/CanDoItAll.Web/Components/Layout/MainLayout.State.cs`
- `repo://src/CanDoItAll.Web/Components/Layout/MainLayout.razor`
- `repo://src/CanDoItAll.Web/Components/Layout/MainLayoutDatabaseDialog.razor`
- `repo://src/CanDoItAll.Web/Program.cs`
- `repo://tests/CanDoItAll.Tests.Unit/DatabaseRuntimeSwitchingTests.cs`
- `repo://tests/CanDoItAll.Tests.Integration/DatabaseRuntimeSwitchingIntegrationTests.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ManagedFilesStorageIntegrationTests.cs`
- `repo://tests/CanDoItAll.Tests.Components/MainLayoutDatabaseProfileTests.cs`
- `repo://tests/CanDoItAll.Tests.Components/SettingsPageDataSourcesTests.cs`
- `repo://tests/CanDoItAll.Tests.Playwright/DatabaseSwitchWorkbenchPlaywrightTests.cs`
- Hash proof: `bundle://proof/SB08/transcripts/changed-file-hashes.txt`

## Command transcripts

- `bundle://proof/SB02/transcripts/unit-runtime-switch-tests.txt`
- `bundle://proof/SB02/transcripts/managed-files-runtime-profile-test-rerun.txt`
- `bundle://proof/SB02/transcripts/playwright-runtime-pending-switch.txt` (failing-first during implementation)
- `bundle://proof/SB02/transcripts/playwright-runtime-pending-switch-rerun.txt` (passing proof)
- `bundle://proof/SB08/transcripts/focused-component-tests.txt`
- `bundle://proof/SB08/transcripts/main-layout-component-tests-rerun.txt`

## Source assertions

- `DatabaseProfileSelectionState` now exposes runtime and pending restart profile identity, descriptor, fingerprint, and pending activation flag.
- `DatabaseProfileWorkspaceService` reads runtime state from `IDatabaseProfileRuntimeAccessor` and pending selection from the catalog instead of conflating them.
- UI labels use `Running now` and `Pending restart`.
- Dev/API DTOs return runtime and pending restart profile ids.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
|---|---|---|---|---|
| Runtime profile state | `repo://src/CanDoItAll.Infrastructure/ControlPlane/DatabaseProfileModels.cs` | `repo://src/CanDoItAll.Modules.Workspace/Database/DatabaseProfileWorkspaceService.cs` | `bundle://proof/SB02/transcripts/unit-runtime-switch-tests.txt` | `bundle://proof/SB02/transcripts/playwright-runtime-pending-switch.txt` |
| Pending restart activation | `repo://src/CanDoItAll.Composition/RuntimeHostServiceCollectionExtensions.cs` | `repo://src/CanDoItAll.Web/Program.cs` and UI files above | `bundle://proof/SB02/transcripts/playwright-runtime-pending-switch-rerun.txt` | `bundle://proof/SB02/transcripts/managed-files-runtime-profile-test-rerun.txt` |

## Semantic positive proof

Unit, focused integration, component, managed-files, and Playwright tests prove that activation records pending restart state without changing the running canonical profile inside the same process.

## Adversarial negative proof

The first Playwright transcript failed while `HasPendingRestartActivation` was not exposed correctly through the dev endpoint; the rerun passes after fixing the endpoint and UI state contract.

## Residual risks

The bundle requested screenshot proof. The current proof is transcript/assertion based; no new screenshot artifact was generated for this bundle.
