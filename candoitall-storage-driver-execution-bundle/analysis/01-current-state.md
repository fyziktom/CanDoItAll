
# Current State

## 1. Baseline storage implementation

- `C:\repositories\CanDoItAll/src/CanDoItAll.Infrastructure/Storage/WorkspaceStorage.cs` contains the current storage abstraction.
- Current public abstractions:
  - `IWorkspacePathResolver`
  - `IWorkspacePathAccessGuard`
  - `IFileStore`
  - `IManagedArtifactStore`
- Current behavior is local-filesystem-only and rooted to the active workspace/profile filesystem.
- The current model assumes relative paths and does not represent remote object locators, provider capabilities, or provider health.
- The current implementation is safe for path traversal in the local profile root, but it does not solve:
  - provider catalog persistence
  - provider-specific configuration
  - per-purpose storage defaults
  - recommendation logic
  - remote preview/download access
  - batch transfer
  - project-structure storage nodes

## 2. Existing configuration and persistence anchors

- `C:\repositories\CanDoItAll/src/CanDoItAll.Infrastructure/Configuration/AppOptions.cs`
  - `StorageOptions` only exposes bootstrap folder names and workspace root.
- `C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Security/SecurityModels.cs`
  - already provides encrypted secret storage and picker-friendly models.
- `C:\repositories\CanDoItAll/src/CanDoItAll.Infrastructure/ControlPlane/DatabaseProfileModels.cs`
  - existing control-plane patterns show how the solution models profile descriptors and storage roots, but the request here is broader than runtime DB switching.
- Both migration projects currently have no storage catalog schema beyond existing media-path fields on workbench entities.

## 3. Current file-write and file-read touchpoints

| Area | Files | Current behavior | Primary gap |
| --- | --- | --- | --- |
| Persisted upload/create | `ProjectWorkbenchModels.cs`, `PromptFactoryService.Pack.cs`, `ProjectStructureImportService.cs`, `ProjectStructurePage.Workflows.cs` | Write bytes into `managed-files/...` and store relative route/path assumptions. | Cannot target remote providers or represent provider/object capabilities. |
| Managed artifact/export | `PromptFactoryService.cs`, `Program.cs`, `TestProfileSeedHelper.cs` | Write text artifacts to local exports/managed-files folders via `IManagedArtifactStore`. | No per-purpose provider routing or storage catalog awareness. |
| Preview/download endpoint | `ManagedFilesEndpointRoutes.cs` | Serves only the active profile filesystem root. | No unified access for IPFS/FTP or capability-driven preview/download decisions. |
| Preview/open UI | `ProjectStructurePage.razor`, `ProjectStructureSelectionPanel.razor`, `ProjectStructureCanvasDialogs.razor`, `PromptSessionAttachmentNode.cs` | Render/open based on `MediaRelativePath` or `MediaRoute` assumptions. | Cannot express remote preview URLs, unsupported local-open, or unified downloads. |
| Host actions | `ProjectStructureLocalFileOpener.cs`, `ProjectStructureRuntimeLauncher.cs` | Trust local filesystem paths under workspace root. | Must remain capability-gated and safe when remote storage exists. |
| Batch/IPFS transport | `DatabaseSnapshots.cs` | Implements ad hoc local/IPFS snapshot transport and copies folder trees manually. | No shared transfer pipeline or provider abstraction. |

## 4. Existing UI patterns relevant to the new storage management surface

- `C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workspace/Pages/SettingsPage.razor`
  - route/tab shell for settings surfaces
- `C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workspace/Pages/Components/DatabaseSourcesSettingsPanel.razor`
  - strong pattern for list/detail shell, test connection, clone/snapshot actions, and health feedback
- `C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Resources/Pages/ResourcesPage.razor`
  - existing input patterns for FTP-ish metadata
- `C:\repositories\CanDoItAll/src/CanDoItAll.Components.BaseLib`
  - best home for reusable presentational components if they are not storage-module-specific

## 5. Existing test and proof assets

- Unit baselines:
  - `C:\repositories\CanDoItAll/tests/CanDoItAll.Tests.Unit/LocalFileStorageTests.cs`
  - `C:\repositories\CanDoItAll/tests/CanDoItAll.Tests.Unit/WorkspacePathResolverGuardTests.cs`
- Integration baselines:
  - `C:\repositories\CanDoItAll/tests/CanDoItAll.Tests.Integration/ManagedFilesStorageIntegrationTests.cs`
  - `C:\repositories\CanDoItAll/tests/CanDoItAll.Tests.Integration/ProfileHarnessIntegrationTests.cs`
- IPFS test harness:
  - `C:\repositories\CanDoItAll/tests/CanDoItAll.Tests.Support/FakeIpfsTestServer.cs`
- Playwright baselines:
  - `C:\repositories\CanDoItAll/tests/CanDoItAll.Tests.Playwright/PlaywrightAppFixture.cs`
  - `C:\repositories\CanDoItAll/tests/CanDoItAll.Tests.Playwright/ProjectStructureArtifactBrowserTests.cs`

## 6. Storage-node and metadata clues already in the repo

- `ProjectRecordingMetadata.StorageReference` already exists in `C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workbench/ProjectWorkbenchMetadata.cs`.
- `ProjectStructureCanvasCatalog.RichDefinitions.cs` already has rich infrastructure node definitions that can absorb a storage-system subtype.
- `ProjectObjectRecord` already has `ExternalArtifactKind` / `ExternalArtifactId`, which can be used or extended for storage-record linking instead of inventing disconnected node bookkeeping.

## 7. Main architectural conclusion from the scan

- This is not a single-file refactor.
- A clean solution needs:
  1. new storage domain contracts and persistence
  2. compatibility seam over `IFileStore` / `IManagedArtifactStore`
  3. provider registry and unified access service
  4. cross-module upload/export adoption
  5. reusable storage UI
  6. deeper test proof plus mandatory manual Playwright MCP review

